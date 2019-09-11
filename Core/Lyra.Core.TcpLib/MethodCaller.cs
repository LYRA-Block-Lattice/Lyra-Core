using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TcpHelperLib
{
    public class MethodCaller
    {
        Dictionary<string, Func<RemoteProcInfo, ProcessingResult>> dctHandler =
            new Dictionary<string, Func<RemoteProcInfo, ProcessingResult>>();

        public Func<RemoteProcInfo, ProcessingResult> this[string name]
        {
            set
            {
                if (!string.IsNullOrEmpty(name) && value != null)
                    dctHandler[name] = value;
            }
        }

        public ProcessingResult ExecuteMethod(RemoteProcInfo rpi)
        {
            const string exceptionPrefix = "ExecuteMethod(): ";
            ProcessingResult result = null;
            if (rpi == null || string.IsNullOrEmpty(rpi.Name))
                throw new Exception($"{exceptionPrefix}Wrong RPI");

            Func<RemoteProcInfo, ProcessingResult> handler = null;
            if (dctHandler.TryGetValue(rpi.Name, out handler) && handler != null)
            {
                try
                {
                    result = handler(rpi);
                }
                catch (Exception e)
                {
                    throw new Exception($"{exceptionPrefix}Exception while executing handler for method \"{rpi.Name}\". ", e);
                }
            }
            else
                throw new Exception($"{exceptionPrefix}No handler for method \"{rpi.Name}\"");

            return result;
        }
    }
}
