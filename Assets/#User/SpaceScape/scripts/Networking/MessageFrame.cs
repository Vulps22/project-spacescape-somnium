using System;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// Somnium's transport carries a bare byte[]; our message id rides in byte 0. The format lives here
    /// rather than at every send site and every handler.
    public static class MessageFrame
    {
        public static byte[] Pack(byte id, byte[] data)
        {
            byte[] framed = new byte[(data?.Length ?? 0) + 1];
            framed[0] = id;
            if (data != null) Buffer.BlockCopy(data, 0, framed, 1, data.Length);
            return framed;
        }

        public static bool Unpack(byte[] framed, out byte id, out byte[] data)
        {
            id = 0;
            data = Array.Empty<byte>();
            if (framed == null || framed.Length == 0) return false;

            id = framed[0];
            data = new byte[framed.Length - 1];
            Buffer.BlockCopy(framed, 1, data, 0, data.Length);
            return true;
        }
    }
}
