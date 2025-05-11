using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LTEK_ULed.Code
{
    public class Settings
    {
        public string? ip { get; set; }
        public List<string>? additionalIps { get; set; }
    }
}
