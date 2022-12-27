using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dls_RabbitMqGateway.Enum
{
    /// <summary>
    /// Exchange types supported by this util lib
    /// </summary>
    public enum ExchangeType
    {
        Topic = 0,
        Fanout = 1,
        // Direct = 2,
        // Header = 3
    }
}
