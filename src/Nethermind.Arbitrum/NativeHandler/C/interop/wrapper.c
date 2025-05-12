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
// __attribute__((visibility("default")))
// UserOutcomeKind stylus_activate_export(
//     GoSliceData wasm,
//     uint16_t page_limit,
//     uint16_t stylus_version,
//     uint64_t arbos_version,
//     bool debug,
//     RustBytes* output,
//     const Bytes32* codehash,
//     Bytes32* module_hash_out,
//     StylusData* stylus_data_out,
//     uint64_t* gas)
// {
//     return stylus_activate(wasm, page_limit, stylus_version, arbos_version, debug,
//                            output, codehash, module_hash_out, stylus_data_out, gas);
// }