namespace Chip8Emulator;

// Chip8 emulator following this guide:
// https://tobiasvl.github.io/blog/write-a-chip-8-emulator/
class Program
{
    #region Emulator Components

    // # of bytes = bits / 8
    byte[] Memory = new byte[4096];
    byte[,] Display = new byte[8, 4];
    ushort ProgramCounter = 0x0200;
    Stack<ushort> ProgramStack = new Stack<ushort>();
    byte DelayTimer = 0x0000;
    byte SoundTimer = 0x0000;

    #region Registers

    ushort IndexRegister = 0x0000;
    byte RegisterV0 = 0x0000;
    byte RegisterV1 = 0x0000;
    byte RegisterV2 = 0x0000;
    byte RegisterV3 = 0x0000;
    byte RegisterV4 = 0x0000;
    byte RegisterV5 = 0x0000;
    byte RegisterV6 = 0x0000;
    byte RegisterV7 = 0x0000;
    byte RegisterV8 = 0x0000;
    byte RegisterV9 = 0x0000;
    byte RegisterVA = 0x0000;
    byte RegisterVB = 0x0000;
    byte RegisterVC = 0x0000;
    byte RegisterVD = 0x0000;
    byte RegisterVE = 0x0000;

    // also used a a 'flag' register to represent 0 or 1
    byte RegisterVF = 0x0000;

    #endregion

    #region Font

    byte[] Font =
    {
        0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
        0x20, 0x60, 0x20, 0x20, 0x70, // 1
        0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
        0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
        0x90, 0x90, 0xF0, 0x10, 0x10, // 4
        0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
        0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
        0xF0, 0x10, 0x20, 0x40, 0x40, // 7
        0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
        0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
        0xF0, 0x90, 0xF0, 0x90, 0x90, // A
        0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
        0xF0, 0x80, 0x80, 0x80, 0xF0, // C
        0xE0, 0x90, 0x90, 0x90, 0xE0, // D
        0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
        0xF0, 0x80, 0xF0, 0x80, 0x80 // F
    };

    #endregion

    #endregion

    public static int _speedInCyclesPerSecond = 1;
    public int clockCyclesCompletedThisSecond = 0;
    public int clockCyclesCompletedTotal = 0;

    static void Main(string[] args)
    {
        Program emulator = new Program();
        emulator.Init();
        emulator.LoadProgram();

        // Main Loop 
        while (true)
        {
            emulator.FetchDecodeExecute();
            emulator.clockCyclesCompletedThisSecond += 1;
            emulator.clockCyclesCompletedTotal += 1;
            //emulator.PrintByteArray(emulator.Memory);
            emulator.DrawScreen();
            Thread.Sleep(1 / _speedInCyclesPerSecond);
            Console.ReadLine();
        }
    }

    // Meta-Emulation Methods
    void Init()
    {
        // Put font in memory
        for (int i = 0x050; i < 0x09F; i++)
        {
            Memory[i] = Font[i - 0x050];
        }
    }

    void FetchDecodeExecute()
    {
        // FETCH
        // an instruction is two bytes 
        byte FirstInstructionByte = Memory[ProgramCounter];
        byte SecondInstructionByte = Memory[(ProgramCounter + 1)];
        // Theres some issue with the endianess or something so the order is reverse
        ushort Instruction = BitConverter.ToUInt16(new byte[2] { SecondInstructionByte, FirstInstructionByte}, 0);
        ProgramCounter += 2;
        Console.WriteLine($"Executing: {Instruction:X4}");
        // DECODE
        DecodeExecute(Instruction);
    }

    void DecodeExecute(ushort Instruction)
    {
        // Nibbles (4-bit num) are in bytes but masked off with AND 
        // so they should only occupy the first 4 bits
        // gotta shift cus you can get the mask but E0 is a big number even though there is a 0
        byte nibbleOne   = (byte)((Instruction & 0xF000) >> 12); // First nibble (highest 4 bits)
        byte nibbleTwo   = (byte)((Instruction & 0x0F00) >> 8);  // Second nibble
        byte nibbleThree = (byte)((Instruction & 0x00F0) >> 4);  // Third nibble
        byte nibbleFour  = (byte)(Instruction & 0x000F);        // Fourth nibble (lowest 4 bits)


        // Nibble one tells us what kind of instruction this is
        switch (nibbleOne)
        {
            case 0x00:
                switch (nibbleTwo)
                {
                    case 0x00:
                        switch (nibbleThree)
                        {
                            case 0x0E:
                                switch (nibbleFour)
                                {
                                    case 0x00:
                                        // 0x00E0
                                        ClearScreen();
                                        break;
                                    default:
                                        ReportInvalidInstruction(Instruction);
                                        break;
                                }

                                break;
                            default:
                                ReportInvalidInstruction(Instruction);
                                break;
                        }

                        break;
                    default:
                        ReportInvalidInstruction(Instruction);
                        break;
                }

                break;
            case 0x01:
                // 0x1NNN (where N is variable: its a memory address)
                ProgramCounter = (ushort)(nibbleTwo + nibbleThree + nibbleFour);
                break;
            case 0x02:
                break;
            case 0x03:
                break;
            case 0x04:
                break;
            case 0x05:
                break;
            case 0x06:
                // 0x6XNN: Set Register VX to NN
                if (nibbleTwo >= 0x0 && nibbleTwo <= 0xF)
                {
                    SetRegister(nibbleTwo, (byte)(nibbleThree + nibbleFour));
                }

                break;
            case 0x07:
                // 0x7XNN: Add NN to Register VX
                if (nibbleTwo >= 0x0 && nibbleTwo <= 0xF)
                {
                    byte currentRegisterVal = GetRegister(nibbleTwo);
                    byte valToAdd = (byte)(nibbleThree + nibbleFour);
                    SetRegister(nibbleTwo, (byte)(valToAdd + currentRegisterVal));
                }

                break;
            case 0x08:
                break;
            case 0x09:
                break;
            case 0x0A:
                // 0xANNN: Set index register to NNN
                if (nibbleTwo >= 0x0 && nibbleTwo <= 0xF)
                {
                    // this is how you compose a 16-bit number from 3 nibbles
                    // note: nibbleTwo is shifted 8 bits and not 12 because the MSB is left
                    // blank as the index register practically only contains 12-bit values
                    // so we leave the first 4 bits blank
                    ushort valToSet = (ushort)((nibbleTwo << 8) | (nibbleThree << 4) | nibbleFour);
                    IndexRegister = valToSet;
                }

                break;
            case 0x0B:
                break;
            case 0x0C:
                break;
            case 0x0D:
                // 0xDXYN:  Display n-byte sprite starting at memory location I at (Vx, Vy), set VF = collision.
                //byte[] bytesRead = [];
                //for (int i = IndexRegister; i < IndexRegister + nibbleFour; i++)
                //{
                    //bytesRead[i] = Memory[i];
                //}
//
                //byte VX = GetRegister(nibbleTwo);
                //byte VY = GetRegister(nibbleThree);

                Display[0, 0] = 1;
                Display[0, 3] = 1;
                Display[7, 0] = 1;
                Display[7, 3] = 1;

                break;
            case 0x0E:
                break;
            case 0x0F:
                break;
            default:
                throw new Exception("Your AND Mask Off did not work");
        }
    }

    void DecrementTimer(TimerType type)
    {
        switch (type)
        {
            case TimerType.SoundTimer:
                if (SoundTimer > 0)
                {
                    SoundTimer = (byte)(SoundTimer - 1);
                }

                break;
            case TimerType.DelayTimer:
                if (DelayTimer > 0)
                {
                    DelayTimer = (byte)(DelayTimer - 1);
                }

                break;
            default:
                throw new Exception("Unknown Timer Type");
        }
    }

    // IO Methods
    void DrawScreen()
    {
        //Console.Clear();
        // Actually Draw Screen
        for (int i = 0; i < Display.GetLength(0); i++)
        {
            for (int j = 0; j < Display.GetLength(1); j++)
            {
                Console.Write("[");
                bool[] bits = ByteToBits(Display[i, j]);
                foreach (var bit in bits)
                {
                    Console.Write(bit ? "1" : "0");
                }

                Console.Write("]");
            }

            Console.WriteLine();
        }

        Console.WriteLine(
            $"Clock Cycle (this second): {clockCyclesCompletedThisSecond} | Clock Cycles Completed: {clockCyclesCompletedTotal}");
    }

    void LoadProgram()
    {
        Console.Write("Enter Program Filename: ");
        string filename = Console.ReadLine() ?? string.Empty;
        // By convention, we will load the program into memory 
        // starting at 0x200 (byte 512)
        byte[] programBytes = File.ReadAllBytes(filename);
        PrintByteArray(programBytes);
        int startingLocationInMemory = 0x200;
        for (int i = 0; i < programBytes.Length; i++)
        {
            Memory[startingLocationInMemory + i] = programBytes[i];
        }
    }

    // ISA Instructions 
    void ClearScreen()
    {
        for (int i = 0; i < Display.GetLength(0); i++)
        {
            for (int j = 0; j < Display.GetLength(1); j++)
            {
                Display[i, j] = 0;
            }
        }
    }

    void SetRegister(byte registerNum, byte registerValue)
    {
        switch (registerNum)
        {
            case 0x0:
                RegisterV0 = registerValue;
                break;
            case 0x1:
                RegisterV1 = registerValue;
                break;
            case 0x2:
                RegisterV2 = registerValue;
                break;
            case 0x3:
                RegisterV3 = registerValue;
                break;
            case 0x4:
                RegisterV4 = registerValue;
                break;
            case 0x5:
                RegisterV5 = registerValue;
                break;
            case 0x6:
                RegisterV6 = registerValue;
                break;
            case 0x7:
                RegisterV7 = registerValue;
                break;
            case 0x8:
                RegisterV8 = registerValue;
                break;
            case 0x9:
                RegisterV9 = registerValue;
                break;
            case 0xA:
                RegisterVA = registerValue;
                break;
            case 0xB:
                RegisterVB = registerValue;
                break;
            case 0xC:
                RegisterVC = registerValue;
                break;
            case 0xD:
                RegisterVD = registerValue;
                break;
            case 0xE:
                RegisterVE = registerValue;
                break;
            case 0xF:
                RegisterVF = registerValue;
                break;
            default:
                ReportInvalidInstruction();
                break;
        }
    }

    byte GetRegister(byte registerNum)
    {
        switch (registerNum)
        {
            case 0x0: return RegisterV0;
            case 0x1: return RegisterV1;
            case 0x2: return RegisterV2;
            case 0x3: return RegisterV3;
            case 0x4: return RegisterV4;
            case 0x5: return RegisterV5;
            case 0x6: return RegisterV6;
            case 0x7: return RegisterV7;
            case 0x8: return RegisterV8;
            case 0x9: return RegisterV9;
            case 0xA: return RegisterVA;
            case 0xB: return RegisterVB;
            case 0xC: return RegisterVC;
            case 0xD: return RegisterVD;
            case 0xE: return RegisterVE;
            case 0xF: return RegisterVF;
            default:
                ReportInvalidInstruction();
                return 0; // will never reach cus of exception
        }
    }

    // Misc 
    void ReportInvalidInstruction(ushort instruction = 0x0)
    {
        throw new Exception("Invalid Instruction: " + instruction);
    }

    bool[] ByteToBits(byte value)
    {
        bool[] bits = new bool[8];
        bits[0] = (value & 0x01) != 0; // Get the bit and check if its == 1
        bits[1] = (value & 0x02) != 0; // Get the bit and check if its == 1
        bits[2] = (value & 0x04) != 0; // Get the bit and check if its == 1
        bits[3] = (value & 0x08) != 0; // Get the bit and check if its == 1
        bits[4] = (value & 0x10) != 0; // Get the bit and check if its == 1
        bits[5] = (value & 0x20) != 0; // Get the bit and check if its == 1
        bits[6] = (value & 0x40) != 0; // Get the bit and check if its == 1
        bits[7] = (value & 0x80) != 0; // Get the bit and check if its == 1

        return bits;
    }

    void PrintByteArray(byte[] arr)
    {
        Console.WriteLine($"--- BEGIN {nameof(arr)} ---");
        for (int i = 0; i < arr.Length; i++)
        {
            Console.Write($"[{i:X4}|");
            if (arr[i] != 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{arr[i]:X4}]");
                Console.ResetColor();
            }
            else
            {
                Console.Write($"{arr[i]:X4}]");
            }

            if (i % 8 == 0)
            {
                Console.WriteLine();
            }
        }

        Console.WriteLine($"--- END {nameof(arr)} ---");
        
    }

    public enum TimerType
    {
        SoundTimer,
        DelayTimer
    }
}