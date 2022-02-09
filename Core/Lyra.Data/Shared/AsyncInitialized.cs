using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.Shared
{
    /// <summary>
    /// Marks a type as requiring asynchronous initialization and provides the result of that initialization.
    /// </summary>
    public interface IAsyncInitialization
    {
        /// <summary>
        /// The result of the asynchronous initialization of this instance.
        /// </summary>
        Task Initialization { get; }
    }

    public abstract class AsyncInitialized : IAsyncInitialization
    {
        IAsyncInitialization? _child;
        public AsyncInitialized()
        {
            Initialization = InitializeAsync();
        }

        public AsyncInitialized(IAsyncInitialization child)
        {
            _child = child;
            Initialization = InitializeAsync();
        }

        public Task Initialization { get; private set; }

        protected virtual async Task InitializeAsync()
        {
            if(_child != null)
                await _child.Initialization;
        }
    }
}
