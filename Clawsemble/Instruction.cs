﻿using System;
using System.Collections.Generic;

namespace Clawsemble
{
    public class Instruction
    {
        public InstructionSignature Signature { get; set; }
        public List<ArgumentToken> Arguments { get; set; }
        public string Label { get; set; }

        public uint File { get; set; }
        public uint Line { get; set; }
        public uint Position{ get; set; }

        public Instruction(uint Position = 0, uint Line = 0, uint File = 0)
        {
            Signature = new InstructionSignature();
            Arguments = new List<ArgumentToken>();
            Label = "";

            this.Position = Position;
            this.Line = Line;
            this.File = File;
        }

        public byte[] Compile(BinaryType Flags)
        {
            return null;
        }
    }
}

