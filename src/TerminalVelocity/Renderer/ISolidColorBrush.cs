﻿/* Copyright (c) Jonathan Dickinson and contributors. All rights reserved.
 * Licensed under the MIT license. See LICENSE file in the project root for details.
*/

using System.Drawing;

namespace TerminalVelocity.Renderer
{
    public interface ISolidColorBrush : IBrush
    {
        Color Color { get; set; }
    }
}
