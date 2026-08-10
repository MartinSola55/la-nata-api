using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Template_Api.Core.Controllers;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Services;
using Template_Api.WhatsApp.Models.DTO;
using Template_Api.WhatsApp.Services;

namespace Template_Api.WhatsApp.Controllers
{
    [Authorize(Policy = Policies.Admin)]
    public class WhatsAppController(WhatsAppService whatsAppService) : BaseController
    {
        private readonly WhatsAppService _whatsAppService = whatsAppService;

        [HttpPost]
        public async Task<BaseResponse<SendTextResponse>> SendText([FromBody] SendTextRequest rq)
        {
            return await _whatsAppService.SendText(rq);
        }
    }
}
