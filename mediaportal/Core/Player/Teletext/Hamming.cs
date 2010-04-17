#region Copyright (C) 2005-2010 Team MediaPortal

// Copyright (C) 2005-2010 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

namespace MediaPortal.Player.Teletext
{
  public class Hamming
  {
    private static byte[] unhamtab =
      {
        0x01, 0xff, 0x81, 0x01, 0xff, 0x00, 0x01, 0xff,
        0xff, 0x02, 0x01, 0xff, 0x0a, 0xff, 0xff, 0x07,
        0xff, 0x00, 0x01, 0xff, 0x00, 0x80, 0xff, 0x00,
        0x06, 0xff, 0xff, 0x0b, 0xff, 0x00, 0x03, 0xff,
        0xff, 0x0c, 0x01, 0xff, 0x04, 0xff, 0xff, 0x07,
        0x06, 0xff, 0xff, 0x07, 0xff, 0x07, 0x07, 0x87,
        0x06, 0xff, 0xff, 0x05, 0xff, 0x00, 0x0d, 0xff,
        0x86, 0x06, 0x06, 0xff, 0x06, 0xff, 0xff, 0x07,
        0xff, 0x02, 0x01, 0xff, 0x04, 0xff, 0xff, 0x09,
        0x02, 0x82, 0xff, 0x02, 0xff, 0x02, 0x03, 0xff,
        0x08, 0xff, 0xff, 0x05, 0xff, 0x00, 0x03, 0xff,
        0xff, 0x02, 0x03, 0xff, 0x03, 0xff, 0x83, 0x03,
        0x04, 0xff, 0xff, 0x05, 0x84, 0x04, 0x04, 0xff,
        0xff, 0x02, 0x0f, 0xff, 0x04, 0xff, 0xff, 0x07,
        0xff, 0x05, 0x05, 0x85, 0x04, 0xff, 0xff, 0x05,
        0x06, 0xff, 0xff, 0x05, 0xff, 0x0e, 0x03, 0xff,
        0xff, 0x0c, 0x01, 0xff, 0x0a, 0xff, 0xff, 0x09,
        0x0a, 0xff, 0xff, 0x0b, 0x8a, 0x0a, 0x0a, 0xff,
        0x08, 0xff, 0xff, 0x0b, 0xff, 0x00, 0x0d, 0xff,
        0xff, 0x0b, 0x0b, 0x8b, 0x0a, 0xff, 0xff, 0x0b,
        0x0c, 0x8c, 0xff, 0x0c, 0xff, 0x0c, 0x0d, 0xff,
        0xff, 0x0c, 0x0f, 0xff, 0x0a, 0xff, 0xff, 0x07,
        0xff, 0x0c, 0x0d, 0xff, 0x0d, 0xff, 0x8d, 0x0d,
        0x06, 0xff, 0xff, 0x0b, 0xff, 0x0e, 0x0d, 0xff,
        0x08, 0xff, 0xff, 0x09, 0xff, 0x09, 0x09, 0x89,
        0xff, 0x02, 0x0f, 0xff, 0x0a, 0xff, 0xff, 0x09,
        0x88, 0x08, 0x08, 0xff, 0x08, 0xff, 0xff, 0x09,
        0x08, 0xff, 0xff, 0x0b, 0xff, 0x0e, 0x03, 0xff,
        0xff, 0x0c, 0x0f, 0xff, 0x04, 0xff, 0xff, 0x09,
        0x0f, 0xff, 0x8f, 0x0f, 0xff, 0x0e, 0x0f, 0xff,
        0x08, 0xff, 0xff, 0x05, 0xff, 0x0e, 0x0d, 0xff,
        0xff, 0x0e, 0x0f, 0xff, 0x0e, 0x8e, 0xff, 0x0e,
      };

    // unham 2 bytes into 1
    public static byte unham(byte low, byte high)
    {
      return (byte)((unhamtab[high] << 4) | (unhamtab[low] & 0x0F));
    }
  }
}