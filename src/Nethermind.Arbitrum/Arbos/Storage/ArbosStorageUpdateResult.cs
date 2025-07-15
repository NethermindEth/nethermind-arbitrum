using System.Diagnostics.CodeAnalysis;

namespace Nethermind.Arbitrum.Arbos.Storage
{
    public readonly struct ArbosStorageUpdateResult(string? error) : IEquatable<ArbosStorageUpdateResult>
    {
        [MemberNotNullWhen(true, nameof(Fail))]
        [MemberNotNullWhen(false, nameof(Success))]
        public string? Error { get; } = error;
        public bool Fail => Error is not null;
        public bool Success => Error is null;

        public static implicit operator ArbosStorageUpdateResult(string? error) => new(error);
        public bool Equals(ArbosStorageUpdateResult other) => (Success && other.Success) || (Error == other.Error);
        public static bool operator ==(ArbosStorageUpdateResult obj1, ArbosStorageUpdateResult obj2) => obj1.Equals(obj2);
        public static bool operator !=(ArbosStorageUpdateResult obj1, ArbosStorageUpdateResult obj2) => !obj1.Equals(obj2);
        public override bool Equals(object? obj) => obj is ArbosStorageUpdateResult result && Equals(result);
        public override int GetHashCode() => Success ? 1 : Error.GetHashCode();

        public override string ToString() => Error is not null ? $"Fail : {Error}" : "Success";

        public static readonly ArbosStorageUpdateResult Ok = new();
        public static readonly ArbosStorageUpdateResult InvalidTime = "Invalid timestamp";
        public static readonly ArbosStorageUpdateResult InsufficientFunds = "Insufficient funds for gas* price + value";
    }
}
