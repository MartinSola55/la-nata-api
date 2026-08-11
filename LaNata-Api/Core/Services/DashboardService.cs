using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using LaNata_Api.Core.DAL.DB;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.DTO.Dashboard;

namespace LaNata_Api.Core.Services
{
    public class DashboardService(APIContext context)
    {
        private readonly APIContext _db = context;

        #region Methods
        public async Task<BaseResponse<GetResponse>> Get()
        {
            return new BaseResponse<GetResponse>
            {
                Data = new GetResponse
                {
                    TotalProducts = await _db.Product.CountAsync()
                }
            };
        }
        #endregion
    }
}
