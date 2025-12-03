using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbMetaTool.Models
{
    public class ScriptLogEntry
    {
        public string ScriptName { get; set; }
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public long DurationMs { get; set; }
    }
}
