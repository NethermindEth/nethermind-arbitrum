using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethermind.Arbitrum.Data;
using Nethermind.Consensus.Producers;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumPayloadAttributes : PayloadAttributes
    {
        public MessageWithMetadata MessageWithMetadata { get; set; }
    }
}
