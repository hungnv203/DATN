using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MovieBooking.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public class PaymentResultController : ControllerBase
{
    [HttpGet("/payment-result")]
    [AllowAnonymous]
    public ContentResult Index()
    {
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["Content-Security-Policy"] =
            "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'";

        const string html = """
            <!doctype html>
            <html lang="vi">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Kết quả thanh toán</title>
              <style>
                :root { color-scheme: dark; }
                * { box-sizing: border-box; }
                body {
                  margin: 0;
                  min-height: 100vh;
                  display: grid;
                  place-items: center;
                  padding: 24px;
                  background: #080d19;
                  color: #f5f7fb;
                  font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
                }
                main { width: min(100%, 480px); text-align: center; }
                .icon {
                  width: 72px;
                  height: 72px;
                  display: grid;
                  place-items: center;
                  margin: 0 auto 24px;
                  border-radius: 50%;
                  background: #173c2c;
                  color: #63e6a5;
                  font-size: 38px;
                  font-weight: 700;
                }
                h1 { margin: 0 0 12px; font-size: 28px; }
                p { margin: 0; color: #b8c0d4; font-size: 17px; line-height: 1.6; }
                .hint { margin-top: 24px; color: #ffffff; font-weight: 600; }
              </style>
            </head>
            <body>
              <main>
                <div class="icon" aria-hidden="true">✓</div>
                <h1>Đã nhận kết quả từ VNPAY</h1>
                <p>Trạng thái giao dịch đang được hệ thống xác minh an toàn.</p>
                <p class="hint">Hãy quay lại ứng dụng và chọn “Kiểm tra trạng thái thanh toán”.</p>
              </main>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }
}
