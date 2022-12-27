using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dls_RabbitMqGateway.Impl
{
    internal static class DisposableExtensions
    {
        /// <summary>
        /// Calls Dispose on disposable object. If exception is thrown during dispose - it is logged as Warn and ignored.
        /// </summary>
        /// <param name="disposable"></param>
        public static void TryDispose(this IDisposable disposable)
        {
            try
            {
                disposable?.Dispose();
            }
            catch
            {
                // It must be ignored
            }
        }

        public static void TryClose(this IConnection connection)
        {
            try
            {
                connection?.Close();
            }
            catch
            {
                // It must be ignored
            }
        }
    }
}
