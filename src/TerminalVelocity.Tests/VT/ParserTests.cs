using System;
using System.Collections.Generic;
using System.Text;
using SourceCode.Clay.Buffers;
using Xunit;

namespace TerminalVelocity.VT
{
    public static class ParserTests
    {
        [Fact]
        public static void Parser_CSI_Populated()
        {
            var packet = Encoding.ASCII.GetBytes("\x1B[1;1;1;1!#p");

            var parser = new Parser();

            var dispatched = 0;
            parser.ControlSequence = new Event<Events.ControlSequenceEvent>(csi =>
            {
                ++dispatched;
                Assert.Equal('p', csi.Character);
                Assert.Equal(IgnoredData.None, csi.Ignored);
                BufferAssert.Equal(new byte[] { 0x21, 0x23 }, csi.Intermediates);
                BufferAssert.Equal(new long[] { 0x01, 0x01, 0x1, 0x1 }, csi.Parameters);
                Assert.Equal("CSI 70 'p' (01; 01; 01; 01) 21; 23", csi.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_CSI_Empty()
        {
            var packet = Encoding.ASCII.GetBytes("\x1B[p");

            var parser = new Parser();

            var dispatched = 0;
            parser.ControlSequence = new Event<Events.ControlSequenceEvent>(csi =>
            {
                ++dispatched;
                Assert.Equal('p', csi.Character);
                Assert.Equal(IgnoredData.None, csi.Ignored);
                Assert.Equal(0, csi.Intermediates.Length);
                Assert.Equal(0, csi.Parameters.Length);
                Assert.Equal("CSI 70 'p' ()", csi.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_CSI_MaxParams_MaxIntermediates()
        {
            var packet = Encoding.ASCII.GetBytes("\x1B[1;1;1;1!#p");

            var parser = new Parser(maxIntermediates: 1, maxParams: 2);

            var dispatched = 0;
            parser.ControlSequence = new Event<Events.ControlSequenceEvent>(csi =>
            {
                ++dispatched;
                Assert.Equal('p', csi.Character);
                Assert.Equal(IgnoredData.All, csi.Ignored);
                BufferAssert.Equal(new byte[] { 0x21 }, csi.Intermediates);
                BufferAssert.Equal(new long[] { 0x01, 0x01 }, csi.Parameters);
                Assert.Equal("CSI 70 'p' (01; 01; ignored) 21; ignored", csi.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_CSI_SemiUnderline()
        {
            var packet = Encoding.ASCII.GetBytes("\x1B[;4m");

            var parser = new Parser();

            var dispatched = 0;
            parser.ControlSequence = new Event<Events.ControlSequenceEvent>(csi =>
            {
                ++dispatched;
                Assert.Equal('m', csi.Character);
                Assert.Equal(IgnoredData.None, csi.Ignored);
                BufferAssert.Equal(new byte[] { }, csi.Intermediates);
                BufferAssert.Equal(new long[] { 0x00, 0x04 }, csi.Parameters);
                Assert.Equal("CSI 6d 'm' (00; 04)", csi.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_CSI_LongParam()
        {
            var packet = Encoding.ASCII.GetBytes("\x1B[9223372036854775808m");

            var parser = new Parser();

            var dispatched = 0;
            parser.ControlSequence = new Event<Events.ControlSequenceEvent>(csi =>
            {
                ++dispatched;
                Assert.Equal('m', csi.Character);
                Assert.Equal(IgnoredData.None, csi.Ignored);
                BufferAssert.Equal(new byte[] { }, csi.Intermediates);
                BufferAssert.Equal(new long[] { long.MaxValue }, csi.Parameters);
                Assert.Equal("CSI 6d 'm' (7fffffffffffffff)", csi.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_DCS_Hook_Put_Unhook()
        {
            var packet = Encoding.ASCII.GetBytes("\x1BP1;2 !@ !X");
            packet[packet.Length - 1] = 0x9C;

            var parser = new Parser();

            var hookDispatched = 0;
            parser.Hook = new Event<Events.HookEvent>(hook =>
            {
                ++hookDispatched;
                Assert.Equal(IgnoredData.None, hook.Ignored);
                BufferAssert.Equal(new long[] { 0x01, 0x02 }, hook.Parameters);
                BufferAssert.Equal(new byte[] { 0x20, 0x21 }, hook.Intermediates);
                Assert.Equal("DCS Hook (01; 02) 20; 21", hook.ToString());
            });

            var putDispatched = 0;
            var putExpected = new byte[] { 0x20, 0x21 };
            parser.Put = new Event<Events.PutEvent>(put =>
            {
                var i = (putDispatched++) % putExpected.Length;
                Assert.Equal(putExpected[i], put.Byte);
                Assert.Equal($"DCS Put {putExpected[i]:x2}", put.ToString());
            });

            var unhookDispatched = 0;
            parser.Unhook = new Event<Events.UnhookEvent>(unhook =>
            {
                unhookDispatched++;
                Assert.Equal("DCS Unhook", unhook.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, hookDispatched);
            Assert.Equal(4, putDispatched);
            Assert.Equal(2, unhookDispatched);
        }
        
        [Fact]
        public static void Parser_DCS_Hook_Put_Unhook_MaxParams_MaxIntermediates()
        {
            var packet = Encoding.ASCII.GetBytes("\x1BP1;2;3 !@ !X");
            packet[packet.Length - 1] = 0x9C;

            var parser = new Parser(maxIntermediates: 1, maxParams: 2);

            var hookDispatched = 0;
            parser.Hook = new Event<Events.HookEvent>(hook =>
            {
                ++hookDispatched;
                Assert.Equal(IgnoredData.All, hook.Ignored);
                BufferAssert.Equal(new long[] { 0x01, 0x02 }, hook.Parameters);
                BufferAssert.Equal(new byte[] { 0x20 }, hook.Intermediates);
                Assert.Equal("DCS Hook (01; 02; ignored) 20; ignored", hook.ToString());
            });

            var putDispatched = 0;
            var putExpected = new byte[] { 0x20, 0x21 };
            parser.Put = new Event<Events.PutEvent>(put =>
            {
                var i = (putDispatched++) % putExpected.Length;
                Assert.Equal(putExpected[i], put.Byte);
                Assert.Equal($"DCS Put {putExpected[i]:x2}", put.ToString());
            });

            var unhookDispatched = 0;
            parser.Unhook = new Event<Events.UnhookEvent>(unhook =>
            {
                unhookDispatched++;
                Assert.Equal("DCS Unhook", unhook.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, hookDispatched);
            Assert.Equal(4, putDispatched);
            Assert.Equal(2, unhookDispatched);
        }

        [Fact]
        public static void Parser_ESC_Populated()
        {
            var packet = Encoding.ASCII.GetBytes("\x1B !0");

            var parser = new Parser();

            var dispatched = 0;
            parser.EscapeSequence = new Event<Events.EscapeSequenceEvent>(esc =>
            {
                ++dispatched;
                Assert.Equal((byte)'0', esc.Byte);
                Assert.Equal(IgnoredData.None, esc.Ignored);
                BufferAssert.Equal(new byte[] { 0x20, 0x21 }, esc.Intermediates);
                Assert.Equal("ESC 30 20; 21", esc.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_ESC_Empty()
        {
            var packet = Encoding.ASCII.GetBytes("\x001B0");

            var parser = new Parser();

            var dispatched = 0;
            parser.EscapeSequence = new Event<Events.EscapeSequenceEvent>(esc =>
            {
                ++dispatched;
                Assert.Equal((byte)'0', esc.Byte);
                Assert.Equal(IgnoredData.None, esc.Ignored);
                BufferAssert.Equal(new byte[] { }, esc.Intermediates);
                Assert.Equal("ESC 30", esc.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_ESC_MaxIntermediates()
        {
            var packet = Encoding.ASCII.GetBytes("\x1B !0");

            var parser = new Parser(maxIntermediates: 1);

            var dispatched = 0;
            parser.EscapeSequence = new Event<Events.EscapeSequenceEvent>(esc =>
            {
                ++dispatched;
                Assert.Equal((byte)'0', esc.Byte);
                Assert.Equal(IgnoredData.Intermediates, esc.Ignored);
                BufferAssert.Equal(new byte[] { 0x20 }, esc.Intermediates);
                Assert.Equal("ESC 30 20; ignored", esc.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_Execute()
        {
            var packet = Encoding.ASCII.GetBytes("\x1C");

            var parser = new Parser();

            var dispatched = 0;
            parser.Execute = new Event<Events.ExecuteEvent>(exec =>
            {
                ++dispatched;
                Assert.Equal(ControlCode.FileSeparator, exec.ControlCode);
                Assert.Equal("Execute FileSeparator", exec.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_OSC_Populated()
        {
            var packet = Encoding.ASCII.GetBytes("\x1B]2;jwilm@jwilm-desk: ~/code/alacritty\x07");

            var parser = new Parser();

            var dispatched = 0;
            parser.OsCommand = new Event<Events.OsCommandEvent>(osc =>
            {
                ++dispatched;
                Assert.Equal(2, osc.Parameters.Length);
                BufferAssert.Equal(osc.Parameters.Span[0], packet.AsMemory(2, 1));
                BufferAssert.Equal(osc.Parameters.Span[1], packet.AsMemory(4, packet.Length - 5));
                
                Assert.Equal("OSC 32; 6a, 77, 69, 6c, 6d, 40, 6a, 77, 69, 6c, 6d, 2d, 64, 65, 73, 6b, 3a, 20, 7e, 2f, 63, 6f, 64, 65, 2f, 61, 6c, 61, 63, 72, 69, 74, 74, 79", osc.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_OSC_Empty()
        {
            var packet = Encoding.ASCII.GetBytes("\x1B]\x07");

            var parser = new Parser();

            var dispatched = 0;
            parser.OsCommand = new Event<Events.OsCommandEvent>(osc =>
            {
                ++dispatched;
                Assert.Equal(1, osc.Parameters.Length);
                Assert.Equal(0, osc.Parameters.Span[0].Length);

                Assert.Equal("OSC ", osc.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_OSC_MaxParams()
        {
            var packet = Encoding.ASCII.GetBytes("\x1B];;;;;;;;;;;;;;;;;\x07");

            var parser = new Parser(maxParams: 3);

            var dispatched = 0;
            parser.OsCommand = new Event<Events.OsCommandEvent>(osc =>
            {
                ++dispatched;
                Assert.Equal(IgnoredData.Parameters, osc.Ignored);
                Assert.Equal(3, osc.Parameters.Length);
                Assert.Equal("OSC ; ; ; ignored", osc.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_OSC_UTF8()
        {
            var packet = Encoding.UTF8.GetBytes("\x1B]2;echo '¯\\_(ツ)_/¯' && sleep 1\x07");

            var parser = new Parser();

            var dispatched = 0;
            parser.OsCommand = new Event<Events.OsCommandEvent>(osc =>
            {
                ++dispatched;
                Assert.Equal(IgnoredData.None, osc.Ignored);
                Assert.Equal(2, osc.Parameters.Length);
                BufferAssert.Equal(osc.Parameters.Span[0], packet.AsMemory(2, 1));
                BufferAssert.Equal(osc.Parameters.Span[1], packet.AsMemory(4, packet.Length - 5));

                Assert.Equal("OSC 32; 65, 63, 68, 6f, 20, 27, c2, af, 5c, 5f, 28, e3, 83, 84, 29, 5f, 2f, c2, af, 27, 20, 26, 26, 20, 73, 6c, 65, 65, 70, 20, 31", osc.ToString());
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(2, dispatched);
        }

        [Fact]
        public static void Parser_Print_UTF8()
        {
            const string Case = "Hello 😁 world ";
            var packet = Encoding.UTF8.GetBytes(Case);

            var parser = new Parser();

            var sb = new StringBuilder();
            var ix = 0;
            parser.Print = new Event<Events.PrintEvent>(print =>
            {
                sb.Append(new string(print.Characters.Span));

                Assert.Equal("Print " + Case.Substring(ix, print.Characters.Length), print.ToString());
                ix = (ix + print.Characters.Length) % Case.Length;
            });

            parser.Process(packet);
            parser.Process(packet);

            Assert.Equal(Case + Case, sb.ToString());
        }
    }
}
