using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using InvoicesProjectEntities.Entities;

namespace InvoicesProjectAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ScriptController : ControllerBase
    {
        [HttpGet("GetScripts")]
        public IActionResult GetScripts()
        {
            var scripts = GenerateCreateTableScripts();
            return Ok(scripts);
        }

        private List<string> GenerateCreateTableScripts()
        {
            var entityTypes = Assembly.GetAssembly(typeof(BaseEntity))
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(BaseEntity)))
                .ToList();

            var scripts = new List<string>();
            foreach (var type in entityTypes)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"CREATE TABLE [{type.Name}] (");
                var props = type.GetProperties();
                foreach (var prop in props)
                {
                    sb.AppendLine($"    [{prop.Name}] {GetSqlType(prop.PropertyType)},");
                }
                sb.Length -= 3; // Remove last comma
                sb.AppendLine("\n);");
                scripts.Add(sb.ToString());
            }
            return scripts;
        }

        private string GetSqlType(System.Type type)
        {
            if (type == typeof(int) || type == typeof(int?)) return "INT";
            if (type == typeof(Guid) || type == typeof(Guid?)) return "UNIQUEIDENTIFIER";
            if (type == typeof(string)) return "NVARCHAR(MAX)";
            if (type == typeof(DateTime) || type == typeof(DateTime?)) return "DATETIME";
            if (type == typeof(bool) || type == typeof(bool?)) return "BIT";
            if (type.IsEnum) return "INT";
            return "NVARCHAR(MAX)";
        }
    }
}
