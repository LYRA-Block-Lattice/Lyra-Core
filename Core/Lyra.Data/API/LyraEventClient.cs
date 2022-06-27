using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Lyra.Core.API;
using Lyra.Data.API;
using Microsoft.AspNetCore.SignalR.Client;

namespace Lyra.Data.API
{
    public class LyraEventHelper
    {
        public static HubConnection CreateConnection(Uri url)
            => new HubConnectionBuilder()
                .WithUrl(url, options =>
                {
                    options.HttpMessageHandlerFactory = (message) =>
                    {
                        if (message is HttpClientHandler clientHandler)
                            // always verify the SSL certificate
                            clientHandler.ServerCertificateCustomValidationCallback +=
                                (sender, certificate, chain, sslPolicyErrors) => { return true; };
                        return message;
                    };
                })
                .WithAutomaticReconnect()
                .Build();
    }

    public class LyraEventClient : ILyraEventRegister, IAsyncDisposable
    {
        private readonly HubConnection _connection;

        public LyraEventClient(HubConnection connection)
        {
            _connection = connection;
            _connection.Closed += Connection_ClosedAsync;
            _connection.Reconnected += Connection_ReconnectedAsync;
            _connection.Reconnecting += Connection_ReconnectingAsync;
        }

        public async Task StartAsync()
        {
            await _connection.StartAsync();
        }

        private Task Connection_ReconnectingAsync(Exception? arg)
        {
            Console.WriteLine($"Reconnecting: {arg}");
            return Task.CompletedTask;
        }

        private Task Connection_ReconnectedAsync(string? arg)
        {
            Console.WriteLine($"Reconnected: {arg}");
            return Task.CompletedTask;
        }

        private Task Connection_ClosedAsync(Exception? arg)
        {
            Console.WriteLine($"Connection closed. {arg?.Message}");
            return Task.CompletedTask;
        }

        public IDisposable RegisterOnEvent(Action<EventContainer> evt)
            => _connection.BindOnInterface(x => x.OnEvent, evt);

        public bool IsConnected => _connection.State == HubConnectionState.Connected;

        public ValueTask DisposeAsync()
        {
            return _connection.DisposeAsync();
        }

        public Task Register(EventRegisterReq msg)
            => _connection.InvokeAsync(nameof(ILyraEventRegister.Register), msg);
    }

    /// <summary> Extension class enables Client code to bind onto the method names and parameters on <see cref="IHubPushMethods"/> with a guarantee of correct method names. </summary>
    public static class HubConnectionBindExtensions
    {
        public static IDisposable BindOnInterface<T>(this HubConnection connection, Expression<Func<ILyraEvent, Func<T, Task>>> boundMethod, Action<T> handler)
            => connection.On<T>(_GetMethodName(boundMethod), handler);

        public static IDisposable BindOnInterface<T1, T2>(this HubConnection connection, Expression<Func<ILyraEvent, Func<T1, T2, Task>>> boundMethod, Action<T1, T2> handler)
            => connection.On<T1, T2>(_GetMethodName(boundMethod), handler);

        public static IDisposable BindOnInterface<T1, T2, T3>(this HubConnection connection, Expression<Func<ILyraEvent, Func<T1, T2, T3, Task>>> boundMethod, Action<T1, T2, T3> handler)
            => connection.On<T1, T2, T3>(_GetMethodName(boundMethod), handler);

        private static string _GetMethodName<T>(Expression<T> boundMethod)
        {
            var unaryExpression = (UnaryExpression)boundMethod.Body;
            var methodCallExpression = (MethodCallExpression)unaryExpression.Operand;
            var methodInfoExpression = (ConstantExpression)methodCallExpression.Object;
            var methodInfo = (MethodInfo)methodInfoExpression.Value;
            return methodInfo.Name;
        }
    }
}
