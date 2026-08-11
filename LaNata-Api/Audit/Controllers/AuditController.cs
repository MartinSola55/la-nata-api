using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using LaNata_Api.Audit.Models.DTO;
using LaNata_Api.Audit.Services;
using LaNata_Api.Core.Controllers;
using LaNata_Api.Core.Models.Constants;
using LaNata_Api.Core.Models.DTO;

namespace LaNata_Api.Audit.Controllers
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
