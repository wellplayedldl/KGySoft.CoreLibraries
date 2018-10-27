﻿#region Copyright

///////////////////////////////////////////////////////////////////////////////
//  File: NullStateUpdater.cs
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
    /// Provides an updater, which does not synchronize command state changes to any command source.
    /// Adding this updater to a <see cref="ICommandBinding"/> ensures that no other updater will be called, which are added after the <see cref="NullStateUpdater"/>.
    /// You can add this updater to a <see cref="ICommandBinding"/> first to disable any other possibly added updater.
    /// </summary>
    /// <seealso cref="ICommandStateUpdater" />
    public class NullStateUpdater : ICommandStateUpdater
    {
        #region Properties

        /// <summary>
        /// Gets a <see cref="NullStateUpdater"/> instance.
        /// </summary>
        public static NullStateUpdater Updater { get; } = new NullStateUpdater();

        #endregion

        #region Constructors

        private NullStateUpdater()
        {
        }

        #endregion

        #region Methods

        void IDisposable.Dispose()
        {
        }

        bool ICommandStateUpdater.TryUpdateState(object commandSource, string stateName, object value) => true;

        #endregion
    }
}
