using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Template_Api.Audit.Models.DTO;
using Template_Api.Audit.Services;
using Template_Api.Core.Controllers;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;

namespace Template_Api.Audit.Controllers
{
    [Authorize(Policy = Policies.Admin)]
    public class AuditController(AuditService auditService) : BaseController
    {
        private readonly AuditService _auditService = auditService;

        #region CRUD
        [HttpPost]
        public async Task<BaseResponse<GetAllResponse>> GetAll([FromBody] GetAllRequest rq)
        {
            return await _auditService.GetAll(rq);
        }

        [HttpGet]
        public async Task<BaseResponse<GetOneResponse>> GetOne([FromQuery] GetOneRequest rq)
        {
            return await _auditService.GetOne(rq);
        }
        #endregion

        #region Combos
        [HttpGet]
        public async Task<BaseResponse<GetFiltersResponse>> GetFilters()
        {
            return await _auditService.GetFilters();
        }
        #endregion
    }
}
