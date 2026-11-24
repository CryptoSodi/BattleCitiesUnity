using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCities.UI
{
    // Mirrors the API's quotedTransaction.js allowlist. The server independently
    // verifies the quote and signature before it can broadcast any payment.
    internal static class ApprovedCheckoutTransaction
    {
        const string Lighthouse = "L2TExMFKdjpN9kozasaurPirfHy9P8sbXoAN1qA3S95";
        const string ComputeBudget = "ComputeBudget111111111111111111111111111111";

        sealed class Instruction
        {
            internal string Program;
            internal string[] Accounts;
            internal byte[] Data;
        }

        sealed class Message
        {
            internal byte Required, ReadonlySigned, ReadonlyUnsigned;
            internal byte[] Blockhash;
            internal string[] Keys;
            internal Instruction[] Instructions;
            internal bool Signer(int index) => index < Required;
            internal bool Writable(int index) => Signer(index)
                ? index < Required - ReadonlySigned
                : index < Keys.Length - ReadonlyUnsigned;
        }

        sealed class Reader
        {
            readonly byte[] bytes;
            int offset;
            internal Reader(byte[] value) { bytes = value; }
            internal byte Byte()
            {
                if (offset >= bytes.Length) throw new FormatException();
                return bytes[offset++];
            }
            internal int Count()
            {
                int value = 0;
                for (int i = 0; i < 3; i++)
                {
                    byte part = Byte();
                    value |= (part & 127) << (7 * i);
                    if ((part & 128) == 0) { if (i > 0 && part == 0) throw new FormatException(); return value; }
                }
                throw new FormatException();
            }
            internal byte[] Bytes(int count)
            {
                if (count < 0 || count > bytes.Length - offset) throw new FormatException();
                var value = new byte[count]; Array.Copy(bytes, offset, value, 0, count); offset += count; return value;
            }
            internal bool Finished => offset == bytes.Length;
        }

        static Message Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length <= 65 || bytes.Length > 1232 || bytes[0] != 1) throw new FormatException();
            var reader = new Reader(bytes);
            reader.Bytes(65); // one signature, followed by the legacy message
            var message = new Message { Required = reader.Byte(), ReadonlySigned = reader.Byte(), ReadonlyUnsigned = reader.Byte() };
            if (message.Required != 1 || message.ReadonlySigned > message.Required) throw new FormatException();
            int keyCount = reader.Count();
            if (keyCount < message.Required || keyCount > 256 || message.ReadonlyUnsigned > keyCount - message.Required) throw new FormatException();
            message.Keys = new string[keyCount];
            var unique = new HashSet<string>();
            for (int i = 0; i < keyCount; i++)
            {
                var key = reader.Bytes(32); message.Keys[i] = CheckoutWalletSigner.Base58(key, 0, key.Length);
                if (!unique.Add(message.Keys[i])) throw new FormatException();
            }
            message.Blockhash = reader.Bytes(32);
            int instructionCount = reader.Count();
            if (instructionCount > bytes.Length) throw new FormatException();
            message.Instructions = new Instruction[instructionCount];
            for (int i = 0; i < instructionCount; i++)
            {
                int program = reader.Byte();
                if (program >= keyCount) throw new FormatException();
                var indexes = reader.Bytes(reader.Count());
                var accounts = new string[indexes.Length];
                for (int j = 0; j < indexes.Length; j++)
                {
                    if (indexes[j] >= keyCount) throw new FormatException();
                    accounts[j] = message.Keys[indexes[j]];
                }
                message.Instructions[i] = new Instruction { Program = message.Keys[program], Accounts = accounts, Data = reader.Bytes(reader.Count()) };
            }
            if (!reader.Finished) throw new FormatException();
            return message;
        }

        internal static bool Matches(byte[] original, byte[] signed) => Matches(original, signed, out _);
        internal static bool Matches(byte[] original, byte[] signed, out string difference)
        {
            difference = "FORMAT";
            try
            {
                var before = Parse(original); var after = Parse(signed);
                difference = "PAYER";
                if (before.Keys[0] != after.Keys[0]) return false;
                difference = "BLOCKHASH";
                if (!before.Blockhash.SequenceEqual(after.Blockhash)) return false;
                difference = "ACCOUNTS";
                for (int i = 0; i < before.Keys.Length; i++)
                {
                    int j = Array.IndexOf(after.Keys, before.Keys[i]);
                    if (j < 0 || before.Signer(i) != after.Signer(j) || before.Writable(i) != after.Writable(j)) return false;
                }
                for (int i = 0; i < after.Keys.Length; i++)
                    if (Array.IndexOf(before.Keys, after.Keys[i]) < 0 && (after.Signer(i) || after.Writable(i))) return false;
                difference = "INSTRUCTIONS";
                int expected = 0, assertions = 0;
                foreach (var instruction in after.Instructions)
                {
                    if (expected < before.Instructions.Length)
                    {
                        var wanted = before.Instructions[expected];
                        if (instruction.Program == wanted.Program && instruction.Accounts.SequenceEqual(wanted.Accounts)
                            && instruction.Data.SequenceEqual(wanted.Data)) { expected++; continue; }
                    }
                    if (instruction.Program != Lighthouse || instruction.Accounts.Length != 1 || instruction.Data.Length == 0
                        || (instruction.Data[0] != 6 && instruction.Data[0] != 10) || ++assertions > 16)
                    {
                        if (instruction.Program == ComputeBudget) difference = "COMPUTE";
                        else if (instruction.Program == Lighthouse) difference = "LIGHTHOUSE";
                        return false;
                    }
                }
                if (expected != before.Instructions.Length) return false;
                difference = null;
                return true;
            }
            catch (Exception error) when (error is FormatException || error is OverflowException || error is ArgumentException) { return false; }
        }
    }
}
