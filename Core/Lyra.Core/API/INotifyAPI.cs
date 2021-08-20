using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public interface INotifyAPI
    {

        // this api simulate push. the node side will hung for 5 min and time out. client should repeat call the api.
        Task<GetNotificationAPIResult> GetNotificationAsync(string AccountID, string Signature);
    }
}
