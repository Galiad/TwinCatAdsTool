using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using CsvHelper;
using TwinCatAdsTool.Interfaces.Models;
using TwinCatAdsTool.Interfaces.Services;

namespace TwinCatAdsTool.Logic.Services
{
    public class CsvHandler : ICsvWriter
    {
       


        public Task<Unit> CreateAndSaveCsv(List<CsvModel> csvList, string path)
        {
            var table = new DataTable();
            using (var writer = new StreamWriter(path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(csvList);
            }

            return Task.FromResult((Unit.Default));
        }
    }
}