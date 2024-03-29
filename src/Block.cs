﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace OneCoin
{
    class Block
    {
        public uint BlockHeight = 1;
        public string PreviousHash = "";
        public string CurrentHash = "";
        public ulong Timestamp = 1;
        public byte Difficulty = 1;
        public string ExtraData = "||";

        public Transaction[] Transactions = Array.Empty<Transaction>();
        public string TransactionsHash;

        public override string ToString()
        {
            if(CheckBlockCorrect())
            {
                string Temp = ExtraData.Split("|")[1];
                if (Temp.Length < 20) { Temp = new string(' ', 20 - Temp.Length) + Temp; }
                string Text = "╔═════════════════════════════╗\n";
                Text += "║ Height: " + new string(' ', 19 - BlockHeight.ToString().Length) + BlockHeight + " ║\n";
                Text += "║ Timestamp: " + new string(' ', 16 - Timestamp.ToString().Length) + Timestamp + " ║\n";
                Text += "║ Difficulty: " + new string(' ', 15 - Difficulty.ToString().Length) + Difficulty + " ║\n";
                Text += "║ Image: Not possible to view ╚" + new string('═', Temp.Length - 17) + "╗\n";
                Text += "║ Signature: " + Temp + " ╚" + new string('═', 208 - Temp.Length) + "╗\n";
                Text += "║ Previous Hash: " + PreviousHash + " ║\n";
                Text += "║ Current Hash:  " + CurrentHash + " ║\n";
                Text += "╠════════════╤══════════════════════════════════════════════════════════════════════════════════════════╤══════════════════════════════════════════════════════════════════════════════════════════╤═══════════════════════════╩╤══════════════════════╗\n";
                Text += "║ Timestamp: │  Address - From:                                                                         │  Address - To:                                                                           │  Amount:                   │  Fee:                ║\n";
                Text += "╟────────────┼──────────────────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────┼────────────────────────────┼──────────────────────╢\n";

                for (int i = 0; i < Transactions.Length; i++)
                {
                    string[] Temps = { Transactions[i].Timestamp.ToString(), Transactions[i].From, Transactions[i].To, Transactions[i].Amount.ToString(), Transactions[i].Fee.ToString(), Transactions[i].Signature };

                    if (Temps[0].Length < 10) { Temps[0] += new string(' ', 10 - Temps[0].Length); }
                    if (Temps[1].Length < 85) { Temps[1] += new string(' ', 88 - Temps[1].Length); }
                    if (Temps[2].Length >= 4 && Temps[2].Length <= 24) { Temps[2] = "[Nickname]: " + Temps[2]; }
                    if (Temps[2].Length >= 128 && Temps[2].Length <= 1048576) { Temps[2] = "[Avatar]: (Avatar Data)"; }
                    if (Temps[2].Length < 85) { Temps[2] += new string(' ', 88 - Temps[2].Length); }
                    if (Temps[3].Length < 24) { Temps[3] = new string('0', 24 - Temps[3].Length) + Temps[3]; }
                    if (Temps[4].Length < 18) { Temps[4] = new string('0', 18 - Temps[4].Length) + Temps[4]; }

                    Text += "║ " + Temps[0] + " │ " + Temps[1] + " │ " + Temps[2] + " │ 0." + Temps[3] + " │ 0." + Temps[4] + " ║\n";
                }

                Text += "╚════════════╧══════════════════════════════════════════════════════════════════════════════════════════╧══════════════════════════════════════════════════════════════════════════════════════════╧════════════════════════════╧══════════════════════╝\n";

                return Text + "* To view block image use another (graphical, not command line) blockchain explorer.";
            }
            return "Block at height " + BlockHeight + " is incorrect!";
        }

        public void RecalculateTransactions()
        {
            TransactionsHash = Transactions[0].Hash();

            for (int i = 1; i < Transactions.Length; i++)
            {
                TransactionsHash = Transactions[i].Hash("", TransactionsHash);
            }
        }

        public void RecalculateHash(bool CalculateTransactionsHash = true, long NodeId = -1)
        { 
            if(CalculateTransactionsHash)
            {
                RecalculateTransactions();
            }

            CurrentHash = Hashing.TqHash(BlockHeight + " " + PreviousHash + " " + Timestamp + " " + Difficulty + " " + ExtraData + " " + TransactionsHash);

            long HistoricalHeight = BlockHeight;
            string HistoryHash = CurrentHash;

            for (int i = 0; i < HistoryHash.Length; i++)
            {
                HistoricalHeight -= ((long)Hashing.HashEncodingIndex(HistoryHash[i]) + 1 + i) * Hashing.Primes[Difficulty + i];
                
                if (HistoricalHeight > 0)
                {
                    if(!Blockchain.BlockExists((uint)HistoricalHeight, NodeId)) { CurrentHash = PreviousHash; break; }
                    Block HistoricalBlock = Blockchain.GetBlock((uint)HistoricalHeight, NodeId);
                    
                    if(HistoricalBlock.Transactions.Length > 0)
                    {
                        long TransactionNumber = BlockHeight + (long)Hashing.SumHash(CurrentHash);

                        for (int j = 0; j < HistoricalBlock.CurrentHash.Length; j++)
                        {
                            TransactionNumber += (long)Hashing.HashEncodingIndex(HistoricalBlock.CurrentHash[j]) * (j + 1);
                        }
                        TransactionNumber %= HistoricalBlock.Transactions.Length;
                        
                        CurrentHash = HistoricalBlock.Transactions[TransactionNumber].Hash(CurrentHash, "");
                    }
                    else
                    {
                        Task.Run(() => Blockchain.FixCorruptedBlocks());
                    }
                }
                else { break; }
            }
        }

        public bool CheckBlockCorrect(long NodeId = -1, byte CustomDifficulty = 0)
        {
            // Mode Types: 1: Only length checking.
            // 2: Hex (Lower), 3: Base32 (Upper),
            // 4: Base64 (No Spaces, No Separators),
            // 5: Base64 (Spaces, No Separators),
            // 6: Base64 (No Spaces, Separators),
            // 7: Base64 (Spaces, Separators)

            if (BlockHeight == 0) { return true; }
            if (BlockHeight == 1) { if(Transactions.Length > 0) { return Transactions[0].Amount == Wallets.MinerRewards[0]; } }

            for (uint i = BlockHeight - 1; i + 10 >= BlockHeight && i != 0; i--)
            {
                if(!Blockchain.BlockExists(i, NodeId)) { if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Missing previous blocks."); } return false; }
            }
            
            Block PreviousBlock = Blockchain.GetBlock(BlockHeight - 1, NodeId);
            bool Correct = Timestamp >= 1647450000;

            RecalculateHash(true, NodeId);
            
            if (PreviousHash == CurrentHash) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Missing historical blocks."); } }
            if (PreviousHash != PreviousBlock.CurrentHash) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Wrong previous hash."); } }
            
            byte TempHashDifficulty = Difficulty;
            if(CustomDifficulty > 1) { TempHashDifficulty = CustomDifficulty; }
            if(!Mining.CheckSolution(CurrentHash, TempHashDifficulty)) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Wrong current hash."); } }

            if (!Hashing.CheckStringFormat(PreviousHash, 3, 205, 205)) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Wrong previous hash format."); } }
            if (!Hashing.CheckStringFormat(CurrentHash, 3, 205, 205)) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Wrong current hash format."); } }
            if (!Hashing.CheckStringFormat(ExtraData, 7, 4096, 4096)) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Wrong extra data format."); } }

            string[] Extras = ExtraData.Split('|');
            if (Extras.Length != 3) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Wrong extras data number."); } }
            if (!Media.ImageDataCorrect(Extras[0])) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Wrong extra data image."); } }

            if (Timestamp <= PreviousBlock.Timestamp) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Wrong timestamp."); } }

            byte NextDifficulty = PreviousBlock.Difficulty;
            
            if (BlockHeight < 11)
            {
                NextDifficulty = (byte)BlockHeight;
            }
            else
            {
                ulong TimestampDifferences = 5;
                bool CanBeChanged = true;
                
                for (uint i = 0; i < 10; i++)
                {
                    if(Blockchain.GetBlock(BlockHeight - (i + 1), NodeId).Difficulty != Blockchain.GetBlock(BlockHeight - (i + 2), NodeId).Difficulty && i != 9)
                    {
                        CanBeChanged = false;
                    }
                    TimestampDifferences += (Blockchain.GetBlock(BlockHeight - (i + 1), NodeId).Timestamp - Blockchain.GetBlock(BlockHeight - (i + 2), NodeId).Timestamp);
                }
                
                if(CanBeChanged)
                {
                    TimestampDifferences /= 10;
                    
                    if (TimestampDifferences < PreviousBlock.Difficulty) { NextDifficulty++; }
                    if (TimestampDifferences > (ulong)PreviousBlock.Difficulty * (ulong)PreviousBlock.Difficulty) { NextDifficulty--; }
                }
            }

            if (Difficulty != NextDifficulty) { Correct = false; if (Program.DebugLogging & Difficulty > 5) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Wrong difficulty."); } }

            Transaction MinerReward = new();
            MinerReward.From = Transactions[0].From;
            MinerReward.To = Transactions[0].To;
            MinerReward.Amount = Wallets.MinerRewards[(BlockHeight-1)/1000000];
            MinerReward.Fee = 0;
            MinerReward.Timestamp = BlockHeight;
            MinerReward.Message = "";
            MinerReward.Signature = BlockHeight + "";

            if (MinerReward + "|" + MinerReward.Signature != Transactions[0] + "|" + Transactions[0].Signature) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Block " + BlockHeight + " is incorrect: Wrong miner reward transaction."); } }

            ulong PreviousTransactionTimestamp = 1;

            Dictionary<string, byte> UserTransactions = new();
            Dictionary<string, BigInteger> UserBalance = new();
            Dictionary<string, bool> SpecialTransaction = new();
            List<string> AlreadyCheckedTransactions = new();

            for (int i = 1; i < Transactions.Length; i++)
            {
                if(Transactions[i].Fee > 0 && BlockHeight < 100000) { Correct = false; }
                
                if (!UserTransactions.ContainsKey(Transactions[i].From))
                {
                    UserTransactions[Transactions[i].From] = 0;
                    UserBalance[Transactions[i].From] = Wallets.GetBalance(Transactions[i].From, NodeId, BlockHeight - 1);
                    SpecialTransaction[Transactions[i].From] = false;
                }
                UserTransactions[Transactions[i].From]++;
                if (UserTransactions[Transactions[i].From] > Difficulty) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Transaction " + i + " in " + BlockHeight + " is incorrect: Too much transactions."); } }
                if (!Transactions[i].CheckTransactionCorrect(UserBalance[Transactions[i].From], BlockHeight, NodeId, i + " in " + BlockHeight)) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Transaction " + i + " in " + BlockHeight + " is incorrect: Wrong transaction."); } }
                UserBalance[Transactions[i].From] -= Transactions[i].Amount;

                if (Transactions[i].Timestamp + 1000000 < Timestamp) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Transaction " + i + " in " + BlockHeight + " is incorrect: Wrong timestamp."); } }
                if (Transactions[i].Timestamp < PreviousTransactionTimestamp) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Transaction " + i + " in " + BlockHeight + " is incorrect: Wrong timestamp."); } }
                if (Transactions[i].Timestamp > Timestamp) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Transaction " + i + " in " + BlockHeight + " is incorrect: Wrong timestamp."); } }
                PreviousTransactionTimestamp = Transactions[i].Timestamp;
                
                if(Transactions[i].To.Length != 88) { SpecialTransaction[Transactions[i].From] = true; }
                if(Transactions[i].Signature.Length == 205) { SpecialTransaction[Transactions[i].From] = true; }
                if(SpecialTransaction[Transactions[i].From] && UserTransactions[Transactions[i].From] > 1) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Transaction " + i + " in " + BlockHeight + " is incorrect: Account data change with transactions."); } }

                if (AlreadyCheckedTransactions.Contains(Transactions[i].Signature)) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Transaction " + i + " in " + BlockHeight + " is incorrect: Already included in this block."); } }
                if (Wallets.CheckTransactionAlreadyIncluded(Transactions[i], NodeId, BlockHeight-1)) { Correct = false; if (Program.DebugLogging) { Console.WriteLine("Transaction " + i + " in " + BlockHeight + " is incorrect: Already included in previous blocks."); } }
                AlreadyCheckedTransactions.Add(Transactions[i].Signature);
            }

            return Correct;
        }
    }
}
