using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TcpHelperLib
{
    public class RemoteProcInfo
    {
        public string Name { get; set; }
        public string CallId { get; set; }
        public object[] Params { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static RemoteProcInfo FromJson(string json)
        {
            return JsonConvert.DeserializeObject<RemoteProcInfo>(json);
        }
    }
}
