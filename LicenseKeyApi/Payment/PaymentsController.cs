using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NanoidDotNet;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

[ApiController]
[Route("api/payment/")]
public partial class PaymentsController : ControllerBase
{
    public static bool IsReadyForPayments = false;

    [HttpGet("getstatus")]
    public ActionResult GetStatus([FromHeader(Name = "X-API-KEY")] string apiKey)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
            return Forbid();

        if (IsReadyForPayments)
            return Ok(new { status = "ready" });
        else
            return StatusCode(503, new { status = "not_ready" });
    }

    [HttpGet("changestatus")]
    public ActionResult ChangeStatus([FromHeader(Name = "X-API-KEY")] string apiKey, [FromQuery] bool ready, [FromServices] IAppLogger logger)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
        {
            logger.Warn("Người dùng không có quyền thay đổi trạng thái hệ thống thanh toán.", new { apiKey } );
            return Forbid();
        }
        if (ready)
        {
            IsReadyForPayments = true;
            logger.Warn("Hệ thống thanh toán đã được bật sẵn sàng hoạt động.");
        }
        else
        {
            logger.Warn("Hệ thống thanh toán đã bị tắt, không còn sẵn sàng hoạt động.");
            IsReadyForPayments = false;
        }
        return Ok(new { status = IsReadyForPayments ? "ready" : "not_ready" });
    }

    [HttpGet("allintent")]
    public async Task<ActionResult<List<PaymentIntent>>> GetAllIntents([FromServices] ApiDbContext db, [FromHeader(Name = "X-API-KEY")] string apiKey, [FromServices] IAppLogger logger)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
        {
            logger.Warn("Người dùng không có quyền truy cập danh sách intent thanh toán.", new { apiKey } );
            return Forbid();
        }    
        var intents = await db.PaymentIntent.ToListAsync();
        return Ok(intents);
    }
    [HttpGet("allpayment")]
    public async Task<ActionResult<List<PaymentIntent>>> GetAllPayments([FromServices] ApiDbContext db, [FromHeader(Name = "X-API-KEY")] string apiKey, [FromServices] IAppLogger logger)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
        {
            logger.Warn("Người dùng không có quyền truy cập danh sách thanh toán.", new { apiKey } );
            return Forbid();
        }
        var intents = await db.Payment.ToListAsync();
        return Ok(intents);
    }
    [HttpGet("alllex")]
    public async Task<ActionResult<List<PaymentIntent>>> GetAlllex([FromServices] ApiDbContext db, [FromHeader(Name = "X-API-KEY")] string apiKey, [FromServices] IAppLogger logger)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
        {
            logger.Warn("Người dùng không có quyền truy cập danh sách License Extension.", new { apiKey } );
            return Forbid();
        }
        var intents = await db.LicenseExtension.ToListAsync();
        return Ok(intents);
    }
    [HttpPost("intent")]
    public async Task<ActionResult<IntentDto>> CreateIntent( [FromBody] IntentReq req,    [FromServices] IPlanCatalog catalog,    [FromServices] ApiDbContext db, [FromServices] IAppLogger log)
    {
        log.Info("Nhận yêu cầu tạo intent", req);

        if (!IsReadyForPayments)
        {
            log.Warn("Từ chối xử lý vì hệ thống thanh toán đang tắt.");
            return StatusCode(503, "Hệ thống thanh toán hiện không khả dụng. Vui lòng thử lại sau.");
        }

        var lic = await db.Licenses.FindAsync(req.LicenseKey);
        if (lic == null)
        {
            log.Warn("License không tồn tại", new { req.LicenseKey });
            return NotFound("License không tồn tại.");
        }

        var plan = catalog.Resolve(req.PlanCode);
        if (plan == null)
        {
            log.Warn("PlanCode không hợp lệ", new { req.PlanCode });
            return BadRequest("PlanCode không hợp lệ.");
        }


        var Now = DateTime.UtcNow.AddHours(7);
        //log.Info("Thực hiện auto-expire intent quá hạn", new { Now });
        // --- Auto-expire các intent đã quá hạn trước khi làm việc tiếp
        await db.PaymentIntent
            .Where(pi => pi.Status == IntentStatus.Pending && pi.ExpiresAtUtc <= Now)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, IntentStatus.Expired));

        // 1) Tìm intent còn hiệu lực để REUSE
        // Nếu muốn “1 intent/pending cho mỗi (license, plan, channel)”
        var existing = await db.PaymentIntent
            .Where(pi => pi.LicenseKey == req.LicenseKey
                        && pi.Channel == req.Channel
                        && pi.Status == IntentStatus.Pending
                        && pi.ExpiresAtUtc > Now)
            .OrderByDescending(pi => pi.CreatedAtUtc) // phòng khi có nhiều bản ghi cũ
            .FirstOrDefaultAsync();

        IntentDto result;

        if (existing != null)
        {
            log.Info("Tìm thấy intent còn hiệu lực → REUSE", new { existing.Id });
            result = new IntentDto
            {
                IntentId = existing.Id,
                PlanCode = existing.PlanCode,
                Amount = existing.AmountVnd,
                QrContent = existing.QrUrl,
                ExpireAt = existing.ExpiresAtUtc
            };
        }
        else
        {
            
            bool isPlanSingle = plan.PlanCode.StartsWith("S_", StringComparison.OrdinalIgnoreCase);
            bool isProduct200v = plan.PlanCode.StartsWith("V_", StringComparison.OrdinalIgnoreCase);
           
            if (lic.Multiversion != null)
            {
              // 🟢 License đã có loại Single/Multi
            bool isSingleLicense = !(bool)lic.Multiversion;

            // ✅ Lấy thời gian hết hạn hiện tại
            DateTime? currentExpire = lic.TimeExpireDaily;
            double remainingDays = currentExpire.HasValue
                ? (currentExpire.Value - Now).TotalDays
                : -1; // <0 nghĩa là đã hết hạn

            bool allowSwitchPlan = remainingDays <= 7;

            // ⚙️ Kiểm tra loại gói: chỉ cấm đổi khi còn hạn > 7 ngày
            if (!allowSwitchPlan)
            {
                if (isSingleLicense && !isPlanSingle && !isProduct200v)
                {
                   log.Warn("Từ chối ...", new 
                   { 
                       req, 
                       remainingDays, 
                       Reason = "License loại Single chỉ được thanh toán các gói Single (chỉ có thể đổi gói khi còn ≤ 7 ngày hoặc đã hết hạn) hoặc gói 200v tối đa 7 ngày."
                   });
                   return StatusCode(StatusCodes.Status403Forbidden,
                        "License loại Single chỉ được thanh toán các gói Single (chỉ có thể đổi gói khi còn ≤ 7 ngày hoặc đã hết hạn) hoặc gói 200v tối đa 7 ngày.");
                }
                if (!isSingleLicense && isPlanSingle)
                {
                    log.Warn("Từ chối ...", new
                    {
                        req,
                        remainingDays,
                        Reason = "License loại Multiversion chỉ được thanh toán các gói Multiversion (chỉ có thể đổi gói khi còn ≤ 7 ngày hoặc đã hết hạn)."
                    });
                    return StatusCode(StatusCodes.Status403Forbidden,
                    "License loại Multiversion chỉ được thanh toán các gói Multiversion (chỉ có thể đổi gói khi còn ≤ 7 ngày hoặc đã hết hạn).");
                }
             }

            // ⭐ RULE 200v cho Single: tối đa 7 ngày
            if (isSingleLicense && isProduct200v)
            {
                DateTime currentExpire200v = lic.TimeExpire200v ?? Now;
                DateTime projectedExpire = currentExpire200v.AddDays(plan.DeltaDays);
                double totalDays = (projectedExpire - Now).TotalDays;

                if (totalDays > 7)
                {
                    if(!allowSwitchPlan)
                    {
                        log.Warn("Từ chối ...", new
                        {
                            req,
                            remainingDays,
                            totalDays,
                            Reason = "Gói 200v (V) chỉ cho phép tối đa 7 ngày cho license Single."
                        });
                        return StatusCode(StatusCodes.Status403Forbidden, $"Gói 200v (V) chỉ cho phép tối đa 7 ngày cho license Single. Dự kiến: {totalDays:F0} ngày.");
                    }    
                   
                }
            }

                // ✅ Nếu là Single → vẫn giữ giới hạn 60 ngày (dù đổi hay không)
             if (isSingleLicense && isPlanSingle)
            {
                DateTime projectedExpire = (currentExpire ?? Now).AddDays(plan.DeltaDays);
                double totalDays = (projectedExpire - Now).TotalDays;

                if (totalDays > 60)
                {
                    log.Warn("Từ chối ...", new
                    {
                        req,
                        remainingDays,
                        totalDays,
                        Reason = "License loại Single chỉ được sử dụng tối đa 60 ngày."
                    });
                    return StatusCode(StatusCodes.Status403Forbidden,$"License loại Single chỉ được sử dụng tối đa 60 ngày. Tổng thời gian dự kiến là {totalDays:F0} ngày.");
                }
            }
            }
            else
            {
                if (isPlanSingle && plan.DeltaDays > 60)
                {
                    log.Warn("Từ chối ...", new
                    {
                        req,
                        Reason = "License mới chưa kích hoạt không được sử dụng gói Single có thời gian quá 60 ngày."
                    });
                    return StatusCode(StatusCodes.Status403Forbidden, "License mới chưa kích hoạt không được sử dụng gói Single có thời gian quá 60 ngày.");
                }
            }
        
            // Chống lạm dụng: giới hạn hủy nhiều lần trong thời gian ngắn
            var cancelThreshold = Now.AddMinutes(-10);
            var cancelCount = await db.PaymentIntent
                .Where(pi => pi.LicenseKey == req.LicenseKey
                            && pi.Status == IntentStatus.Canceled
                            && pi.CreatedAtUtc >= cancelThreshold)
                .CountAsync();

            if (cancelCount >= 3)
            {
                log.Warn("Từ chối tạo intent mới do hủy quá nhiều lần trong thời gian ngắn", new { req.LicenseKey, cancelCount });
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { error = "Bạn đã hủy thanh toán quá 3 lần trong 10 phút. Vui lòng thử lại sau." });
            }

            if (req.PlanCode == "S_1D")
            {
                log.Warn("Từ chối tạo intent mới cho gói S_1D (bị loại bỏ)", new { req.LicenseKey });
                return StatusCode(StatusCodes.Status403Forbidden, "Gói S_1D không còn được hỗ trợ.");
            }
            // 2) Không có -> tạo mới
            string baseID;
            do
            {
                baseID = $"PI_{Nanoid.Generate("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 10)}";
            } 
            while (db.PaymentIntent.Any(p => p.Id == baseID));
            var intent = new PaymentIntent
            {
                Id = baseID,
                LicenseKey = req.LicenseKey,
                PlanCode = plan.PlanCode,
                AmountVnd = plan.Price,
                Channel = req.Channel,
                ProviderTxnId = "",
                TransferNote = "",
                Status = IntentStatus.Pending,
                CreatedAtUtc = Now,
                ExpiresAtUtc = Now.AddMinutes(10)
            };

            string qrContent = $"{intent.Id}"; // đủ để webhook tra PI rồi đối soát
            intent.QrUrl =
                $"https://qr.sepay.vn/img?bank=VPBank&acc=0392388090&template=compact&amount={intent.AmountVnd}&des={Uri.EscapeDataString(qrContent)}";

            db.PaymentIntent.Add(intent);
            await db.SaveChangesAsync();

            log.Info("Tạo intent mới thành công", new { intent.Id });

            result = new IntentDto
            {
                IntentId = intent.Id,
                PlanCode = plan.PlanCode,
                Amount = intent.AmountVnd,
                QrContent = intent.QrUrl,
                ExpireAt = intent.ExpiresAtUtc
            };
        }
       
        // Serialize object JSON
        var jsonData = System.Text.Json.JsonSerializer.Serialize(result);

        // Mã hóa AES
        var encryptedLicense = Security.EncryptData(jsonData, out string ivBase64);

        return Ok(new { data = encryptedLicense, data2 = ivBase64 });

    }

    [HttpPost("intent/cancel")]
    public async Task<IActionResult> CancelIntent([FromBody] IntentDto _It,  [FromServices] ApiDbContext db,    CancellationToken ct, [FromServices] IAppLogger logger)
    {
        logger.Info("Nhận yêu cầu hủy intent", new { _It.IntentId });

        if (_It is null || string.IsNullOrEmpty(_It.IntentId))
        {
            logger.Warn("Payload hủy intent không hợp lệ.");
            return BadRequest("Payload không hợp lệ.");

        }

        // lấy intent kèm rowversion để tracking
        var it = await db.PaymentIntent 
                         .FirstOrDefaultAsync(x => x.Id == _It.IntentId, ct);
        if (it == null) 
        {
            logger.Warn("Intent hủy không tồn tại.", new { _It.IntentId });
            return NotFound("Intent không tồn tại.");
        } 

        var now = DateTime.UtcNow.AddHours(7);

        // Nếu đã hết hạn mà Status vẫn Pending -> tự chuyển Expired (idempotent)
        if (it.Status == IntentStatus.Pending && it.ExpiresAtUtc <= now)
        {
            it.Status = IntentStatus.Expired;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                logger.Warn("Xung đột trạng thái khi tự động chuyển intent đã hết hạn sang Expired.");
                return Conflict("Xung đột trạng thái. Vui lòng thử lại.");
            }
            logger.Info("Intent đã hết hạn, tự động chuyển trạng thái sang Expired.", new { it.Id });
            // 410 Gone mang nghĩa “không còn hiệu lực”
            return StatusCode(StatusCodes.Status410Gone, new { it.Id, it.Status });
        }

        // Ý nghĩa idempotent: nếu đã canceled -> OK trả trạng thái hiện tại
        if (it.Status == IntentStatus.Canceled)
        {
            logger.Info("Intent đã ở trạng thái Canceled, trả về trạng thái hiện tại.", new { it.Id });
            return Ok(new { it.Id, it.Status });
        }

        // Không cho hủy khi đã thành công / đã hết hạn
        if (it.Status is IntentStatus.Succeeded or IntentStatus.Expired)
        {

            logger.Warn("Không thể hủy intent ở trạng thái hiện tại.", new { it.Id, it.Status } );
            return BadRequest("Không thể hủy intent ở trạng thái hiện tại.");

        }

        // Trường hợp hợp lệ: Pending & chưa hết hạn -> hủy
        it.Status = IntentStatus.Canceled;

        try
        {
            await db.SaveChangesAsync(ct);
            logger.Info("Hủy intent thành công.", new { it.Id });
            return Ok(new { it.Id, it.Status });
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.Warn("Xung đột trạng thái khi hủy intent.", new { it.Id });
            // Có tiến trình khác vừa cập nhật (webhook thanh toán/chủ thể khác hủy)
            return Conflict("Xung đột trạng thái. Vui lòng tải lại và thử lại.");
        }
    }

    [HttpGet("intent/{id}")]
    public async Task<ActionResult<IntentStatusDto>> GetIntentStatus([FromRoute] string id,[FromServices] ApiDbContext db, CancellationToken ct, [FromServices] IAppLogger logger)
    {
        // 1) Validate input (tránh brute-force/ID format bất thường)
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
        {
            logger.Warn("Yêu cầu trạng thái intent với ID không hợp lệ.", new { id });
            return NotFound(); // tránh lộ thông tin
        }

        if (!Regex.IsMatch(id, @"^PI_[A-Za-z0-9]{10}$"))
        {
            logger.Warn("Yêu cầu trạng thái intent với ID không đúng định dạng.", new { id });
            return NotFound();
        }
        // 2) Tải intent "no-tracking" để giảm lock
        var intent = await db.PaymentIntent
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (intent == null) 
        { 
            logger.Warn("Yêu cầu trạng thái intent không tồn tại.", new { id });
            return NotFound(); 
        }

        // Auto-expire nếu quá hạn
        if (intent.Status == IntentStatus.Pending && DateTime.UtcNow.AddHours(7) > intent.ExpiresAtUtc)
        {
            intent.Status = IntentStatus.Expired;
            await db.SaveChangesAsync();
        }

        var now = DateTime.UtcNow.AddHours(7);
        var effectiveStatus = intent.Status;
        if (effectiveStatus == IntentStatus.Pending && now > intent.ExpiresAtUtc)
        {
            effectiveStatus = IntentStatus.Expired;
        }

        var data =  new IntentStatusDto
        {
            Status = intent.Status.ToString()
        };
        // Serialize object JSON
        var jsonData = System.Text.Json.JsonSerializer.Serialize(data);

        // Mã hóa AES
        var encryptedLicense = Security.EncryptData(jsonData, out string ivBase64);

        return Ok(new { data = encryptedLicense, data2 = ivBase64 });
    }

    [HttpPost("sepay/webhook")]
    public async Task<IActionResult> SepayWebhook([FromBody] WebhookReq req,[FromServices] ApiDbContext db,[FromServices] IPlanCatalog catalog,[FromServices] IConfiguration cfg, [FromServices] IAppLogger logger)
    {
        logger.Info("Nhận webhook từ Sepay", req);
        // 1) Lấy raw body
        using var reader = new StreamReader(Request.Body);
        req.RawBody = await reader.ReadToEndAsync();

        // 2) Verify signature nếu có
        var secret = cfg["Sepay:WebhookSecret"];
        var authHeader = Request.Headers["Authorization"].ToString();

        if(authHeader != "Apikey cothatsulasepaykhongvaycha")
        {
            logger.Warn("Xác thực webhook thất bại.", new { authHeader });
            return Unauthorized();
        }

        // 3) Lấy PI từ content
        var intentId = ExtractIntentId(req.code);
        if (string.IsNullOrEmpty(intentId))
        {
            logger.Warn("Không tìm thấy intentId trong mã chuyển khoản.", new { req.code , length = req.code.Length });
            await LogEarlyPayment(db, req, "Không tìm thấy intentId trong mã chuyển khoản");
            return StatusCode(201, new { success = true, reason = "No intentId" });
        }

        using var tx = await db.Database.BeginTransactionAsync();
        var intent = await db.PaymentIntent.FindAsync(intentId);
        if (intent == null)
        {
            logger.Warn("Không tìm thấy intentId trong mã chuyển khoản chính.", new { intentId, req.code });
            intentId = ExtractIntentId( ExtractIntentIdFromWebhook(req) );
            intent = await db.PaymentIntent.FindAsync(intentId);
            if (intent == null)
            {
                logger.Warn("Không tìm thấy intentId trong mã chuyển khoản phụ.", new { intentId, req.description });
                await LogEarlyPayment(db, req, "Intent không tồn tại");
                await tx.CommitAsync();
                return StatusCode(201, new { success = true, reason = "Intent does not exist" });
            }
            logger.Info("Đã tìm thấy intentId trong mã chuyển khoản phụ.", new { intentId , req.description });
        }

        if (intent.Status != IntentStatus.Pending)
        {
            logger.Warn("Intent không ở trạng thái Pending.", new { intent.Id, intent.Status });
            await LogEarlyPayment(db, req, "Intent không ở trạng thái Pending");
            await tx.CommitAsync();
            return StatusCode(201, new { success = true, reason = "Intent not pending" });
        }
        string PaymenID = (db.Payment.Count() + 1).ToString();
        if ((long)req.transferAmount < intent.AmountVnd)
        {
            db.Payment.Add(new Payment
            {
                Id = PaymenID,
                IntentId = intent.Id,
                AmountVnd = (long)req.transferAmount,
                Provider = "Sepay",
                ExternalId = req.id.ToString(),
                ReferenceCode = req.referenceCode,
                PaidAtUtc = DateTime.ParseExact(req.transactionDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                PaymentNote = "Số tiền thanh toán không đủ.",
            });
            
            logger.Warn("Số tiền thanh toán không đủ.", new { intent.Id, intent.AmountVnd, PaidAmount = req.transferAmount });  

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return StatusCode(201, new { success = true });
        }

        intent.Status = IntentStatus.Succeeded;
        intent.ProviderTxnId = req.id.ToString();
        string PaymentNote = "";
        if ((long)req.transferAmount > intent.AmountVnd)
        {
            logger.Warn("Số tiền thanh toán lớn hơn yêu cầu.", new { intent.Id, intent.AmountVnd, PaidAmount = req.transferAmount });   
            PaymentNote = $"Số tiền thanh toán lớn hơn yêu cầu {(long)req.transferAmount - intent.AmountVnd}";
        }
        else
        {
            PaymentNote = "Thanh toán đầy đủ.";
        }
        db.Payment.Add(new Payment
        {
            Id = PaymenID,
            IntentId = intent.Id,
            AmountVnd = (long)req.transferAmount,
            Provider = "Sepay",
            ExternalId = req.id.ToString(),
            ReferenceCode = req.referenceCode,
            PaidAtUtc = DateTime.ParseExact(req.transactionDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            PaymentNote = PaymentNote,
        });
        logger.Info("Thanh toán thành công.", new { intent.Id, intent.AmountVnd, PaidAmount = req.transferAmount });
        var plan = catalog.Resolve(intent.PlanCode);
        if (plan != null)
        {
            var lic = await db.Licenses.FindAsync(intent.LicenseKey);
            if (lic != null)
            {
                var Now = DateTime.UtcNow.AddHours(7);

                bool isPlanSingle = plan.PlanCode.StartsWith("S", StringComparison.OrdinalIgnoreCase);
                bool isPlanMulti = plan.PlanCode.StartsWith("M", StringComparison.OrdinalIgnoreCase)
                                || plan.PlanCode.StartsWith("V", StringComparison.OrdinalIgnoreCase)
                                || plan.PlanCode.StartsWith("C", StringComparison.OrdinalIgnoreCase);

                var currentExpire = lic.TimeExpireDaily;
                double remainingDays = currentExpire.HasValue ? (currentExpire.Value - Now).TotalDays : -1;
                bool allowSwitchPlan = remainingDays <= 7; // cho đổi nếu còn ≤7 ngày hoặc hết hạn

                // Xác định loại license hiện tại (Single/Multi)
                bool? isCurrentSingle = lic.Multiversion == null ? null : !(bool)lic.Multiversion;

                // Nếu license chưa xác định loại → xác định dựa theo plan hiện tại
                if (isCurrentSingle == null)
                {
                    if (isPlanSingle)
                    {
                        lic.Multiversion = false;
                        lic.TimeExpireDaily = Now.AddDays(plan.DeltaDays);
                    }
                    else
                    {
                        // License mới với gói Multi/V/C
                        lic.Multiversion = true;

                        if (plan.PlanCode.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                        {
                            lic.TimeExpire200v = Now.AddDays(plan.DeltaDays);
                        }
                        else if (plan.PlanCode.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                        {
                            lic.TimeExpireDaily = Now.AddDays(plan.DeltaDays);
                            lic.TimeExpire200v = Now.AddDays(plan.DeltaDays);
                        }
                        else
                        {
                            lic.TimeExpireDaily = Now.AddDays(plan.DeltaDays);
                        }
                    }
                }
                else
                {
                    // Đã có loại → kiểm tra đổi gói
                    bool isSingleLicense = isCurrentSingle.Value;

                    // Nếu đổi loại và được phép (≤7 ngày hoặc hết hạn) → reset thời gian
                    bool switchingType = (isSingleLicense && isPlanMulti) || (!isSingleLicense && isPlanSingle);
                    // if (switchingType && allowSwitchPlan)
                    // {
                    //     currentExpire = Now; // reset
                    // }

                    // Áp dụng theo từng nhóm gói
                    if (isPlanSingle)
                    {
                        // Giới hạn 60 ngày kể cả khi gia hạn
                        DateTime projectedExpire = (currentExpire ?? Now).AddDays(plan.DeltaDays);
                        // double totalDays = (projectedExpire - Now).TotalDays;
                        // if (totalDays > 60)
                        //     throw new InvalidOperationException("License Single không được vượt quá 60 ngày.");

                        lic.TimeExpireDaily = projectedExpire;

                        lic.Multiversion = false;
                    }
                    else if (plan.PlanCode.StartsWith("M", StringComparison.OrdinalIgnoreCase))
                    {
                        lic.TimeExpireDaily = (currentExpire != null && currentExpire > Now)
                            ? currentExpire.Value.AddDays(plan.DeltaDays)
                            : Now.AddDays(plan.DeltaDays);
                        lic.Multiversion = true;
                    }
                    else if (plan.PlanCode.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                    {
                        if(isSingleLicense)
                        {
                            // RULE 200v cho Single: tối đa 7 ngày
                            DateTime currentExpire200v = lic.TimeExpire200v ?? Now;
                            DateTime projectedExpire = currentExpire200v.AddDays(plan.DeltaDays);
                            double totalDays = (projectedExpire - Now).TotalDays;
                            lic.TimeExpire200v = projectedExpire;
                            if(allowSwitchPlan)
                                lic.Multiversion = true;
                        }
                        else
                        {
                            lic.TimeExpire200v = (lic.TimeExpire200v != null && lic.TimeExpire200v >  Now)
                                    ? lic.TimeExpire200v.Value.AddDays(plan.DeltaDays)
                                    : Now.AddDays(plan.DeltaDays);
                            lic.Multiversion = true;
                        }
                    }
                    else if (plan.PlanCode.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                    {
                        lic.TimeExpireDaily = (lic.TimeExpireDaily != null && lic.TimeExpireDaily >  Now)
                            ? lic.TimeExpireDaily.Value.AddDays(plan.DeltaDays)
                            : Now.AddDays(plan.DeltaDays);

                        lic.TimeExpire200v = (lic.TimeExpire200v != null && lic.TimeExpire200v > Now)
                            ? lic.TimeExpire200v.Value.AddDays(plan.DeltaDays)
                            : Now.AddDays(plan.DeltaDays);

                        lic.Multiversion = true;
                    }
                    else if((plan.PlanCode.StartsWith("T", StringComparison.OrdinalIgnoreCase)))
                    {
                        lic.TimeExpireAoMaThap = (lic.TimeExpireAoMaThap != null && lic.TimeExpireAoMaThap > Now)
                            ? lic.TimeExpireAoMaThap.Value.AddDays(plan.DeltaDays)
                            : Now.AddDays(plan.DeltaDays);
                    }
                    else if((plan.PlanCode.StartsWith("N", StringComparison.OrdinalIgnoreCase)))
                    {
                        lic.TimeExpireNoel = (lic.TimeExpireNoel != null && lic.TimeExpireNoel > Now)
                            ? lic.TimeExpireNoel.Value.AddDays(plan.DeltaDays)
                            : Now.AddDays(plan.DeltaDays);
                    }
                }

                logger.Info("Cập nhật license sau thanh toán thành công.", new
                {
                    lic.Name,
                    lic.LicenseKey,
                    lic.Multiversion,
                    lic.TimeExpireDaily,
                    lic.TimeExpire200v,
                    lic.TimeExpireAoMaThap,
                    lic.TimeExpireNoel
                });
                // Ghi log gia hạn
                var ext = new LicenseExtension
                {
                    Id = $"LEX_{db.LicenseExtension.Count()}",
                    LicenseKey = lic.LicenseKey,
                    PlanCode = plan.PlanCode,
                    DeltaDays = plan.DeltaDays,
                    SourcePaymentId = PaymenID
                };

                db.LicenseExtension.Add(ext);
            }
        }
        try
        {

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return StatusCode(201, new { success = true });
        }
        catch (Exception ex)
        {
            logger.Error("Xử lý webhook thất bại.", ex);
            await tx.RollbackAsync();
            return StatusCode(500, new { success = false, error = ex.Message });
        }
     
    }
    // Helpers
    private static bool VerifyHmac(string raw, string sig, string secret)
    {
        // tuỳ cổng; ví dụ HMAC-SHA256 hex
        using var h = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
        var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return string.Equals(hex, sig?.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
    }
    string ExtractIntentIdFromWebhook(WebhookReq req)
    {
        // 1) Quick exact search in description/content
        var exact = FirstMatch(req.description) ?? FirstMatch(req.content);
        if (!string.IsNullOrEmpty(exact)) return exact;

        string content = req.content ?? "";
        string code = req.code ?? "";

        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(code))
            return null;

        // Tạo regex cho phép chèn khoảng trắng vào giữa các ký tự mã
        string pattern = BuildFlexiblePattern(code);

        var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        // Lấy phần từ vị trí bắt đầu mã trở đi
        string tail = req.content.Substring(match.Index);

        // Loại bỏ mọi khoảng trắng để lấy chuỗi liền
        string alnum = string.Concat(tail.Where(char.IsLetterOrDigit));

        // Trả kết quả với đoạn 12 ký tự
        return alnum.Substring(0,12);

        string FirstMatch(string s)
        {
            if (string.IsNullOrEmpty(s)) return null; 
            var mm = Regex.Match(s, @"PI[A-Za-z0-9]{10}", RegexOptions.IgnoreCase);
            return mm.Success ? mm.Value : null;
        }
        string BuildFlexiblePattern(string code)
        {
            // Cho phép khoảng trắng giữa bất kỳ ký tự nào
            return string.Join("", code.Select(c => $"{Regex.Escape(c.ToString())}\\s*"));
        }
    }

    private async Task LogEarlyPayment(ApiDbContext db, WebhookReq req, string note)
    {
        string id = (db.Payment.Count() + 1).ToString();

        db.Payment.Add(new Payment
        {
            Id = id,
            IntentId = null, // không có intent
            AmountVnd = (long)req.transferAmount,
            Provider = "Sepay",
            ExternalId = req.id.ToString(),
            ReferenceCode = req.referenceCode,
            PaidAtUtc = DateTime.UtcNow,
            PaymentNote = note
        });

        await db.SaveChangesAsync();
    }

    private static string? ExtractIntentId(string desc)
    {
        // parse chuỗi chứa "PI:<id>"
        // ví dụ "PI:abc123|AMT:100000|LK:..."
        if (string.IsNullOrEmpty(desc)) return null;
        var parts = desc.Split('|');
        foreach (var p in parts)
        {
            if (p.StartsWith("PI", StringComparison.OrdinalIgnoreCase))
                return "PI_"+p.Substring(2).Trim();
        }
        return null;
    }
}
public class IntentReq
{
    public string LicenseKey { get; set; } = default!;
    public string PlanCode { get; set; } = default!;
    public string Channel { get; set; } = "SEEPAY";
}

public class IntentDto
{
    public string IntentId { get; set; } = default!;
    public string PlanCode { get; set; } = default!;
    public long Amount { get; set; }
    public string QrContent { get; set; } = default!;
    public DateTime ExpireAt { get; set; }
}


public class IntentStatusDto
{
    public string Status { get; set; } = default!;  // PENDING/PAID/EXPIRED/CANCELED
    // public long? PaidAmount { get; set; }
    // public DateTime? ExpriePaidAt { get; set; }
}
public class WebhookReq
{
    public long id { get; set; }

    public string gateway { get; set; }

    public string transactionDate { get; set; }

    public string accountNumber { get; set; }

    public string? code { get; set; }

    public string content { get; set; }

    public string transferType { get; set; }

    public decimal transferAmount { get; set; }

    public decimal accumulated { get; set; }

    public string? subAccount { get; set; }

    public string referenceCode { get; set; }

    public string description { get; set; }

    // Nếu bạn muốn lưu raw body để verify HMAC
    [JsonIgnore]
    public string RawBody { get; set; } = "";
}
public enum IntentStatus { Pending = 0, Succeeded = 1, Expired = 2, Canceled = 3 }