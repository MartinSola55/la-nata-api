using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Services;
using Template_Api.MercadoPago.Models.DTO;
using Template_Api.MercadoPago.Services;

namespace Template_Api.MercadoPago.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class MercadoPagoController(MercadoPagoService mercadoPagoService) : ControllerBase
    {
        private readonly MercadoPagoService _mercadoPagoService = mercadoPagoService;

        [HttpGet]
        [Authorize]
        public async Task<BaseResponse<GetPaymentResponse>> GetPayment([FromQuery] GetPaymentRequest rq)
        {
            return await _mercadoPagoService.GetPayment(rq);
        }

        [HttpPost]
        [Authorize]
        public async Task<BaseResponse<CreatePreferenceResponse>> CreatePreference([FromBody] CreatePreferenceRequest rq)
        {
            return await _mercadoPagoService.CreatePreference(rq);
        }

        [HttpPost]
        [Authorize]
        public async Task<BaseResponse<CreatePreferenceResponse>> CreateCartPreference([FromBody] CreateCartPreferenceRequest rq)
        {
            return await _mercadoPagoService.CreateCartPreference(rq);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<BaseResponse<ProcessWebhookResponse>> Webhook([FromBody] MercadoPagoWebhookRequest rq)
        {
            return await _mercadoPagoService.ProcessWebhook(rq, Request);
        }
    }
}
