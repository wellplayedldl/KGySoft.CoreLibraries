﻿#region Copyright

///////////////////////////////////////////////////////////////////////////////
//  File: ICommandSource.cs
///////////////////////////////////////////////////////////////////////////////
//  Copyright (C) KGy SOFT, 2005-2018 - All Rights Reserved
//
//  You should have received a copy of the LICENSE file at the top-level
//  directory of this distribution. If not, then this file is considered as
//  an illegal copy.
//
//  Unauthorized copying of this file, via any medium is strictly prohibited.
///////////////////////////////////////////////////////////////////////////////

#endregion

#region Usings

using System;

#endregion

namespace KGySoft.ComponentModel
{
    /// <summary>
    /// Represents source information about the command.
    /// <br/>See the <strong>Remarks</strong> section of the <see cref="ICommand"/> interface for details and examples about commands.
    /// </summary>
    public interface ICommandSource
    {
        #region Properties

        /// <summary>
        /// Gets the source if the invocation, which is the object that triggered the event.
        /// </summary>
        object Source { get; }

        /// <summary>
        /// Gets the triggering event of the source object.
        /// </summary>
        string TriggeringEvent { get; }

        /// <summary>
        /// Gets the <see cref="System.EventArgs"/> instance containing the event data.
        /// </summary>
        EventArgs EventArgs { get; }

        #endregion
    }
}