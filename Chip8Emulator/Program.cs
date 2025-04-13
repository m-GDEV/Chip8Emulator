using Raylib_cs;
using System.Timers;

namespace Chip8Emulator;

// Chip8 emulator following this guide:
// https://tobiasvl.github.io/blog/write-a-chip-8-emulator/
class Program
{
    #region Emulator Components

    // # of bytes = bits / 8
    byte[] Memory = new byte[4096];
    bool[,] Display = new bool[32, 64];
    ushort ProgramCounter = 0x0200;
    Stack<ushort> ProgramStack = new Stack<ushort>();
    byte DelayTimer = 0x0000;
    byte SoundTimer = 0x0000;
    KeyboardKey pressedKey;

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

    #region Chip 8 Key Mapping
    Dictionary<KeyboardKey, byte> keymap = new()
    {
        // CHIP-8: 1 2 3 C       → Keyboard: 1 2 3 4
        { KeyboardKey.One,   0x1 },
        { KeyboardKey.Two,   0x2 },
        { KeyboardKey.Three, 0x3 },
        { KeyboardKey.Four,  0xC },

        // CHIP-8: 4 5 6 D       → Keyboard: Q W E R
        { KeyboardKey.Q,     0x4 },
        { KeyboardKey.W,     0x5 },
        { KeyboardKey.E,     0x6 },
        { KeyboardKey.R,     0xD },

        // CHIP-8: 7 8 9 E       → Keyboard: A S D F
        { KeyboardKey.A,     0x7 },
        { KeyboardKey.S,     0x8 },
        { KeyboardKey.D,     0x9 },
        { KeyboardKey.F,     0xE },

        // CHIP-8: A 0 B F       → Keyboard: Z X C V
        { KeyboardKey.Z,     0xA },
        { KeyboardKey.X,     0x0 },
        { KeyboardKey.C,     0xB },
        { KeyboardKey.V,     0xF },
    };
    

    #endregion

    #endregion

    public int clockCyclesCompletedThisSecond = 0;
    public int clockCyclesCompletedTotal = 0;
    
    public static int _speedInCyclesPerSecond = 1000;
    private static System.Timers.Timer mainTimer60hz;
    public static int _resolutionScale = 2;
    public static Sound beep;

    static void Main(string[] args)
    {
        Program emulator = new Program();
        emulator.Init();
        emulator.LoadProgram();
        
        // Raylib setup
        Raylib.InitWindow(640 * _resolutionScale, 320 * _resolutionScale, "Chip8 Emulator");
        Raylib.InitAudioDevice();
        beep = Raylib.LoadSound("beep.wav");
        Raylib.PlaySound(beep);
        Raylib.SetTargetFPS(_speedInCyclesPerSecond);

        // Main Loop 
        while (!Raylib.WindowShouldClose())
        {
            // We need to use a global variable for the currently pressed key 
            // as it needs to happen in the Raylib loop. 
            emulator.pressedKey = (KeyboardKey)Raylib.GetKeyPressed();
            
            emulator.FetchDecodeExecute();
            
            // Update clock cycle counts
            emulator.clockCyclesCompletedThisSecond += 1;
            emulator.clockCyclesCompletedTotal += 1;

            if (emulator.clockCyclesCompletedTotal % _speedInCyclesPerSecond == 0)
            {
                emulator.clockCyclesCompletedThisSecond = 0;
            }
            
            // Apply internal state of display to Raylib window
            // Internal display is 64x32 so it is scaled accordingly for Raylib
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.White);

            for (int i = 0; i < emulator.Display.GetLength(0); i++)
            {
                for (int j = 0; j < emulator.Display.GetLength(1); j++)
                {
                    if (emulator.Display[i, j])
                    {
                        for (int k = 0; k < 10; k++)
                        {
                            Raylib.DrawRectangle(j * 10 * _resolutionScale, i * 10 * _resolutionScale, 10 * _resolutionScale, 10 * _resolutionScale, Color.Black);
                        }
                    }
                }
            }
            
            Raylib.EndDrawing();
            
            Console.Clear();
            Console.WriteLine(
                $"Clock Cycle (this second): {emulator.clockCyclesCompletedThisSecond} | Clock Cycles Completed: {emulator.clockCyclesCompletedTotal} | Instruction: {emulator.Memory[emulator.ProgramCounter - 2]}");
            Thread.Sleep((int)(1000.0 / _speedInCyclesPerSecond));
        }

        // Raylib de-init
        Raylib.UnloadSound(beep);
        Raylib.CloseAudioDevice();
        Raylib.CloseWindow();
    }

    // Meta-Emulation Methods
    void Init()
    {
        // Put font in memory
        for (int i = 0x050; i < 0x09F; i++)
        {
            Memory[i] = Font[i - 0x050];
        }
        
        // Start both Delay and Sound timers that run independently of the clock at 60hz
        StartTimers();
    }

    void FetchDecodeExecute()
    {
        // FETCH
        // an instruction is two bytes 
        byte FirstInstructionByte = Memory[ProgramCounter];
        byte SecondInstructionByte = Memory[(ProgramCounter + 1)];
        // Theres some issue with the endianess or something so the order is reverse
        ushort Instruction = BitConverter.ToUInt16(new byte[2] { SecondInstructionByte, FirstInstructionByte }, 0);
        ProgramCounter += 2;
        // Console.WriteLine($"Executing: {Instruction:X4}");
        // DECODE
        DecodeExecute(Instruction);
    }

    void DecodeExecute(ushort Instruction)
    {
        // Nibbles (4-bit num) are in bytes but masked off with AND 
        // so they should only occupy the first 4 bits
        // gotta shift cus you can get the mask but E0 is a big number even though there is a 0
        byte nibbleOne = (byte)((Instruction & 0xF000) >> 12); // First nibble (highest 4 bits)
        byte nibbleTwo = (byte)((Instruction & 0x0F00) >> 8); // Second nibble
        byte nibbleThree = (byte)((Instruction & 0x00F0) >> 4); // Third nibble
        byte nibbleFour = (byte)(Instruction & 0x000F); // Fourth nibble (lowest 4 bits)

        // Nibble one tells us what kind of instruction this is
        switch (nibbleOne)
        {
            case 0x00:
            {
                switch (nibbleTwo)
                {
                    case 0x00:
                    {
                        switch (nibbleThree)
                        {
                            case 0x0E:
                            {
                                switch (nibbleFour)
                                {
                                    case 0x00:
                                    {
                                        // 0x00E0
                                        ClearScreen();
                                        break;
                                    }
                                    case 0x0E:
                                    {
                                        // 0x00EE
                                        ProgramCounter = ProgramStack.Pop();
                                        break;
                                    }
                                    default:
                                    {
                                        ReportInvalidInstruction(Instruction);
                                        break;
                                    }
                                }

                                break;
                            }
                            default:
                            {
                                ReportInvalidInstruction(Instruction);
                                break;
                            }
                        }

                        break;
                    }
                    default:
                    {
                        ReportInvalidInstruction(Instruction);
                        break;
                    }
                }

                break;
            }
            case 0x01:
            {
                // 0x1NNN: Jump (Make program counter NNN)
                ProgramCounter = (ushort)((nibbleTwo << 8) | (nibbleThree << 4) | (nibbleFour));
                break;
            }
            case 0x02:
            {
                // 0x2NNN: Call Subroutine (Make program counter NNN and save old PC to stack)
                ProgramStack.Push(ProgramCounter);
                ProgramCounter = (ushort)((nibbleTwo << 8) | (nibbleThree << 4) | (nibbleFour));
                break;
            }
            case 0x03:
            {
                // 0x3XNN: BCC (Do nothing or increment program counter if VX == NN)
                byte nn = (byte)((nibbleThree << 4) | nibbleFour);
                if (GetRegister(nibbleTwo) == nn)
                {
                    ProgramCounter += 2;
                }

                break;
            }
            case 0x04:
            {
                // 0x3XNN: BCC (Do nothing or increment program counter if VX != NN)
                byte nn2 = (byte)((nibbleThree << 4) | nibbleFour);
                if (GetRegister(nibbleTwo) != nn2)
                {
                    ProgramCounter += 2;
                }

                break;
            }
            case 0x05:
            {
                if (nibbleFour == 0)
                {
                    // 0x5XY0: BCC (Do nothing or increment program counter if VX == VY)
                    if (GetRegister(nibbleTwo) == GetRegister(nibbleThree))
                    {
                        ProgramCounter += 2;
                    }
                }

                break;
            }
            case 0x06:
            {
                // 0x6XNN: Set Register VX to NN
                byte val = (byte)((nibbleThree << 4) | nibbleFour);
                SetRegister(nibbleTwo, val);
                break;
            }
            case 0x07:
            {
                // 0x7XNN: Add NN to Register VX
                byte currentRegisterVal = GetRegister(nibbleTwo);
                byte valToAdd = (byte)((nibbleThree << 4) | nibbleFour);
                SetRegister(nibbleTwo, (byte)(valToAdd + currentRegisterVal));

                break;
            }
            case 0x08:
            {
                switch (nibbleFour)
                {
                    case 0x0:
                    {
                        // 0x8XY0: Set VX equal to VY
                        SetRegister(nibbleTwo, GetRegister(nibbleThree));
                        break;
                    }
                    case 0x1:
                    {
                        // 0x8XY1: Set VX equal to VX OR VY
                        var vx = GetRegister(nibbleTwo);
                        var vy = GetRegister(nibbleThree);
                        SetRegister(nibbleTwo, (byte)(vx | vy));
                        break;
                    }
                    case 0x2:
                    {
                        // 0x8XY2: Set VX equal to VX AND VY
                        var vx = GetRegister(nibbleTwo);
                        var vy = GetRegister(nibbleThree);
                        SetRegister(nibbleTwo, (byte)(vx & vy));
                        break;
                    }
                    case 0x3:
                    {
                        // 0x8XY3: Set VX equal to VX XOR VY
                        var vx = GetRegister(nibbleTwo);
                        var vy = GetRegister(nibbleThree);
                        SetRegister(nibbleTwo, (byte)(vx ^ vy));
                        break;
                    }
                    case 0x4:
                    {
                        // 0x8XY4: VX = VX + VY
                        var vx = GetRegister(nibbleTwo);
                        var vy = GetRegister(nibbleThree);
                        var res = vx + vy;

                        if (res > 255)
                        {
                            RegisterVF = 1; // Set carry flag
                        }
                        SetRegister(nibbleTwo, (byte)res);
                        
                        break;
                    }
                    case 0x5:
                    {
                        // 0x8XY5: VX = VX - VY
                        var vx = GetRegister(nibbleTwo);
                        var vy = GetRegister(nibbleThree);
                        var res = vx - vy;

                        if (vx > vy)
                        {
                            RegisterVF = 1; // Set borrow flag
                        }
                        SetRegister(nibbleTwo, (byte)res);
                        
                        break;
                    }
                    case 0x6:
                    {
                        // 0x8XY6: Right shift and set VF
                        //     VX = VY (optional, disabled by default)
                        //     Shift VX one bit right
                        //     Set VF to 1 if the bit that was shifted was 1, or 0 if it was 0
                        
                        // SetRegister(nibbleTwo, GetRegister(nibbleThree));
                        var vx = GetRegister(nibbleTwo);
                        var bit = ByteToBits(vx)[0]; // LSB
                        
                        SetRegister(nibbleTwo, (byte) (vx >> 1));

                        RegisterVF = bit ? (byte)1 : (byte)0;
                        
                        break;
                    }
                    case 0x7:
                    {
                        // 0x8XY5: VX = VY - VX
                        var vx = GetRegister(nibbleTwo);
                        var vy = GetRegister(nibbleThree);
                        var res = vy - vx;

                        if (vy > vx)
                        {
                            RegisterVF = 1; // Set borrow flag
                        }
                        SetRegister(nibbleTwo, (byte)res);
                        
                        break;
                    }
                    case 0xE:
                    {
                        // 0x8XYE: Left shift and set VF
                        //     VX = VY (optional, disabled by default)
                        //     Shift VX one bit left
                        //     Set VF to 1 if the bit that was shifted was 1, or 0 if it was 0
                        
                        // SetRegister(nibbleTwo, GetRegister(nibbleThree));
                        var vx = GetRegister(nibbleTwo);
                        var bit = ByteToBits(vx)[7]; // MSB
                        
                        SetRegister(nibbleTwo, (byte) (vx << 1));

                        RegisterVF = bit ? (byte)1 : (byte)0;
                        
                        break;
                    }
                }

                break;
            }
            case 0x09:
            {
                if (nibbleFour == 0)
                {
                    // 0x9XY0: BCC (Do nothing or increment program counter if VX != VY)
                    if (GetRegister(nibbleTwo) != GetRegister(nibbleThree))
                    {
                        ProgramCounter += 2;
                    }
                }

                break;
            }
            case 0x0A:
            {
                // 0xANNN: Set index register to NNN
                if (nibbleTwo >= 0x0 && nibbleTwo <= 0xF)
                {
                    // this is how you compose a 16-bit number from 3 nibbles
                    // note: nibbleTwo is shifted 8 bits and not 12 because the most significant nibble is left
                    // blank as the index register practically only contains 12-bit values
                    // so we leave the first 4 bits blank
                    ushort valToSet = (ushort)((nibbleTwo << 8) | (nibbleThree << 4) | nibbleFour);
                    IndexRegister = valToSet;
                }

                break;
            }
            case 0x0B:
            {
                // BNNN / BXNN: Jump to V0 + NNN / Jump to VX + XNN 
                
                // Case 1 (default)
                var num = (byte)((nibbleTwo << 8) | (nibbleThree << 4) | nibbleFour);
                var v0 = GetRegister(0);
                ProgramCounter = (byte)(num + v0);
                // Case 2 (can be enabled, disabled by default)
                // var num = (byte)((nibbleTwo << 8) | (nibbleThree << 4) | nibbleFour);
                // var vx = GetRegister(nibbleTwo);
                // ProgramCounter = (byte)(num + vx);
                break;
            }
            case 0x0C:
            {
                // CXNN: Generate random number, AND it with NN, and put result in VX
                var num2 = (byte)((nibbleThree << 4) | nibbleFour); // NN
                var r = new Random();
                var num = r.Next(0, 255); // Possible numbers a byte can have (NN is two nibble AKA one byte)
                var res = (byte)((byte)num ^ num2);
                
                SetRegister(nibbleTwo, res);
            }
                break;
            case 0x0D:
            {
                // 0xDXYN:  Display n-byte sprite starting at memory location I at (Vx, Vy), set VF = collision.
                var x = GetRegister(nibbleTwo);
                var y = GetRegister(nibbleThree);
                RegisterVF = 0;

                for (int row = 0; row < nibbleFour; row++)
                {
                    byte spriteByte = Memory[IndexRegister + row];
                    int pixelY = (y + row) % 32;

                    for (int bit = 0; bit < 8; bit++)
                    {
                        int pixelX = (x + bit) % 64;
                        // spriteByte >> (7 - bit) -> shift the desired bit to the right most posistion 
                        // & 1 -> mask everything except that one bit 
                        // == 1 -> convert the bit into a bool
                        bool spritePixel = ((spriteByte >> (7 - bit)) & 1) == 1;

                        if (spritePixel)
                        {
                            if (Display[pixelY, pixelX])
                            {
                                RegisterVF = 1;
                            }

                            // XOR the pixel (equivalent to Display[pixelY, pixelX] = Display[pixelY, pixelX] XOR 1
                            Display[pixelY, pixelX] ^= true;
                        }
                    }
                }

                break;
            }
            case 0x0E:
            {
                if (nibbleThree == 0x9 && nibbleFour == 0xE)
                {
                   // EX9E: Skip an instruction (increment PC by 2) if the value of the key being pressed is equal to VX
                   var vx = GetRegister(nibbleTwo);
                   var f = Raylib.GetKeyPressed();
                   if (Raylib.GetKeyPressed() == vx)
                   {
                       ProgramCounter += 2;
                   }
                }
                else if (nibbleThree == 0xA && nibbleFour == 0x1)
                {
                   // EXA1: Skip an instruction (increment PC by 2) if the value of the key being pressed is NOT equal to VX
                   var vx = GetRegister(nibbleTwo);
                   var f = Raylib.GetKeyPressed();
                   if (Raylib.GetKeyPressed() != vx)
                   {
                       ProgramCounter += 2;
                   }
                }
                else
                {
                    ReportInvalidInstruction(Instruction);
                }
                break;
            }
            case 0x0F:
            {
                if (nibbleThree == 0x0 && nibbleFour == 0x7)
                {
                    // 0xFX07: VX = DelayTimer
                    SetRegister(nibbleTwo, DelayTimer); 
                }
                if (nibbleThree == 0x1 && nibbleFour == 0x5)
                {
                    // 0xFX15: DelayTimer = VX
                    DelayTimer = GetRegister(nibbleTwo);
                }
                if (nibbleThree == 0x1 && nibbleFour == 0x8)
                {
                    // 0xFX18: SoundTimer = VX
                    SoundTimer = GetRegister(nibbleTwo);
                }

                if (nibbleThree == 0x1 && nibbleFour == 0xE)
                {
                    // 0xFX1E: Add IndexRegister += VX
                    var vx = GetRegister(nibbleTwo);
                    IndexRegister += vx; // idk what happens if it overflows (i.e. 0x1000, because its a 12bit addressable space)
                    if (IndexRegister >= 0x1000)
                    {
                        RegisterVF = 1;
                    }
                }

                if (nibbleThree == 0x0 && nibbleFour == 0xA)
                {
                    // 0xFX0A: Get key from user, key value put into VX
                    //         if no keys pressed, decrement program counter and re-run instruction
                    if (keymap.TryGetValue(pressedKey, out byte chip8Key))
                    {
                        SetRegister(nibbleTwo, chip8Key);
                    }
                    else
                    {
                        ProgramCounter -= 2;
                    }
                }
                if (nibbleThree == 0x2 && nibbleFour == 0x9)
                {
                    // 0xFX29: Set IndexRegister to address of the character of the least significant nibble of VX
                    // The font begins in memory at 0x50 (by convention)
                    // Each character of the font (0-F) is 5 bytes
                    var vx = GetRegister(nibbleTwo);
                    var val = (0x0F & vx);
                    if (val < 0x0 || val > 0xF)
                    {
                        throw new Exception("Oopsie daisy");
                    }

                    IndexRegister = (byte)(0x50 + (5 * val));
                }

                if (nibbleThree == 0x3 && nibbleFour == 0x3)
                {
                    // 0xFX33: Take VX, convert it to decimal, place each digit (there are three) of the result 
                    //         into memory at Memory[IndexRegister], Memory[IndexRegister + 1], and Memory[IndexRegister + 2]
                    var vx = GetRegister(nibbleTwo);
                    var hundreds= vx / 100; 
                    var tens =(vx / 10) % 10;
                    var ones = vx % 10;

                    Memory[IndexRegister] = (byte)hundreds;
                    Memory[IndexRegister + 1] = (byte)tens;
                    Memory[IndexRegister + 2] = (byte)ones;
                }

                if (nibbleThree == 0x5 && nibbleFour == 0x5)
                {
                    // 0XFX55: takes values from V0 to VX and stores them successively in memory starting at Memory[IndexRegister]
                    for (int i = 0; i <= nibbleTwo; i++)
                    {
                        Memory[IndexRegister + i] = GetRegister((byte)i);
                    }
                }
                
                if (nibbleThree == 0x6 && nibbleFour == 0x5)
                {
                    // 0XFX65: takes N values from Memory[IndexRegister] ... Memory[IndexRegister + N] and stores them successively in V0 - VN
                    for (int i = 0; i <= nibbleTwo; i++)
                    {
                        SetRegister((byte)i, Memory[IndexRegister + i]);
                    }
                }
                break;
            }
            default:
            {
                throw new Exception("Your AND Mask Off did not work");
            }
        }
    }

    void StartTimers()
    {
        mainTimer60hz = new System.Timers.Timer(1000.0 / 60); // runs at 60hz 
        mainTimer60hz.Enabled = true;
        mainTimer60hz.Elapsed += TimerTick;
        mainTimer60hz.AutoReset = true; 
        mainTimer60hz.Start();
    }

    void TimerTick(object? send, ElapsedEventArgs e)
    {
        if (DelayTimer > 0)
        {
            DelayTimer = (byte)(DelayTimer - 1);
        }

        if (SoundTimer > 0)
        {
            SoundTimer = (byte)(SoundTimer - 1);
            Raylib.PlaySound(beep);
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
                // Console.Write("[");
                Console.Write(Display[i, j] ? "X" : "_");
                // Console.Write("]");
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
                Display[i, j] = false;
            }
        }
    }

    void SetRegister(byte registerNum, byte registerValue)
    {
        switch (registerNum)
        {
            case 0x0:
            {
                RegisterV0 = registerValue;
                break;
            }
            case 0x1:
            {
                RegisterV1 = registerValue;
                break;
            }
            case 0x2:
            {
                RegisterV2 = registerValue;
                break;
            }
            case 0x3:
            {
                RegisterV3 = registerValue;
                break;
            }
            case 0x4:
            {
                RegisterV4 = registerValue;
                break;
            }
            case 0x5:
            {
                RegisterV5 = registerValue;
                break;
            }
            case 0x6:
            {
                RegisterV6 = registerValue;
                break;
            }
            case 0x7:
            {
                RegisterV7 = registerValue;
                break;
            }
            case 0x8:
            {
                RegisterV8 = registerValue;
                break;
            }
            case 0x9:
            {
                RegisterV9 = registerValue;
                break;
            }
            case 0xA:
            {
                RegisterVA = registerValue;
                break;
            }
            case 0xB:
            {
                RegisterVB = registerValue;
                break;
            }
            case 0xC:
            {
                RegisterVC = registerValue;
                break;
            }
            case 0xD:
            {
                RegisterVD = registerValue;
                break;
            }
            case 0xE:
            {
                RegisterVE = registerValue;
                break;
            }
            case 0xF:
            {
                RegisterVF = registerValue;
                break;
            }
            default:
            {
                ReportInvalidInstruction();
                break;
            }
        }
    }

    byte GetRegister(byte registerNum)
    {
        switch (registerNum)
        {
            case 0x0:
            {
                return RegisterV0;
            }
            case 0x1:
            {
                return RegisterV1;
            }
            case 0x2:
            {
                return RegisterV2;
            }
            case 0x3:
            {
                return RegisterV3;
            }
            case 0x4:
            {
                return RegisterV4;
            }
            case 0x5:
            {
                return RegisterV5;
            }
            case 0x6:
            {
                return RegisterV6;
            }
            case 0x7:
            {
                return RegisterV7;
            }
            case 0x8:
            {
                return RegisterV8;
            }
            case 0x9:
            {
                return RegisterV9;
            }
            case 0xA:
            {
                return RegisterVA;
            }
            case 0xB:
            {
                return RegisterVB;
            }
            case 0xC:
            {
                return RegisterVC;
            }
            case 0xD:
            {
                return RegisterVD;
            }
            case 0xE:
            {
                return RegisterVE;
            }
            case 0xF:
            {
                return RegisterVF;
            }
            default:
            {
                ReportInvalidInstruction();
                return 0; // will never reach cus of exception
            }
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
}