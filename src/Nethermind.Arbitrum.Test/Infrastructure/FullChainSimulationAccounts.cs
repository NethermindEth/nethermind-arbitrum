using Nethermind.Crypto;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public class FullChainSimulationAccounts
{
    // Captured from Full Chain Simulation by running:
    // docker compose run scripts print-private-key --account ... - supported accounts: l2owner, funnel, sequencer

    // Address: 0x5e1497dd1f08c87b2d8fe23e9aab6c1de833d927
    public static readonly PrivateKey Owner = new("dc04c5399f82306ec4b4d654a342f40e2e0620fe39950d967e1e574b32d4dd36");

    // Address: 0x3f1eae7d46d88f08fc2f8ed27fcb2ab183eb2d0e
    public static readonly PrivateKey Funnel = new("b6b15c8cb491557369f3c7d2c287b053eb229daa9c22138887752191c9520659");

    // Address 0xe2148ee53c0755215df69b2616e552154edc584f
    public static readonly PrivateKey Sequencer = new("cb5790da63720727af975f42c79f69918580209889225fa7128c92402a6d3a65");

    // 0x3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E
    public static readonly PrivateKey Dev = new("0xb6b15c8cb491557369f3c7d2c287b053eb229daa9c22138887752191c9520659");

    // Just random accounts for testing purposes
    public static readonly PrivateKey AccountA = new("010102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
    public static readonly PrivateKey AccountB = new("020102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
    public static readonly PrivateKey AccountC = new("030102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
    public static readonly PrivateKey AccountD = new("040102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
    public static readonly PrivateKey AccountE = new("050102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
    public static readonly PrivateKey AccountF = new("060102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
}
