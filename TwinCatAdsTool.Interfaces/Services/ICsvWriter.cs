using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using TwinCatAdsTool.Interfaces.Models;

namespace TwinCatAdsTool.Interfaces.Services
{
    public interface ICsvWriter
    {
        Task<Unit> CreateAndSaveCsv(List<CsvModel> csvList, string path);
       
    }
}