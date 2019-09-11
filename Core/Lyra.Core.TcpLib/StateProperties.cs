using System.Collections.Concurrent;

namespace TcpHelperLib
{
    public class StateProperties
    {
        private ConcurrentDictionary<string, object> cdctState = new ConcurrentDictionary<string, object>();

        public object this[string propName]
        {
            get
            {
                object ob = null;
                cdctState.TryGetValue(propName, out ob);

                return ob;
            }
            set
            {
                if (!string.IsNullOrEmpty(propName))
                {
                    if (value != null)
                        cdctState[propName] = value;
                    else
                    {
                        object ob = null;
                        cdctState.TryRemove(propName, out ob);
                    }
                }
            }
        }

        public RemoteProcInfo GetRpi(string procName)
        {
            return this[procName] as RemoteProcInfo;
        }
    }
}
