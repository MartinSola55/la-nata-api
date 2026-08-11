using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.DTO.Dashboard;
using LaNata_Api.Core.Services;

namespace LaNata_Api.Core.Controllers
{
    public class DashboardController(DashboardService dashboardService) : BaseController
    {
        private readonly DashboardService _dashboardService = dashboardService;

        #region Methods
        [HttpGet]
        public async Task<BaseResponse<GetResponse>> Get()
        {
            return await _dashboardService.Get();
        }
        #endregion
    }
}
