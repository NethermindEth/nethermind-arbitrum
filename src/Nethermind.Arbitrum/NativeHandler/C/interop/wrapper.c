#include "arbitrator.h"

__attribute__((visibility("default")))
UserOutcomeKind stylus_call_export(
    GoSliceData module,
    GoSliceData calldata,
    StylusConfig config,
    NativeRequestHandler req_handler,
    EvmData evm_data,
    bool debug,
    RustBytes* output,
    uint64_t* gas,
    uint32_t arbos_tag)
{
    return stylus_call(module, calldata, config, req_handler, evm_data, debug, output, gas, arbos_tag);
}