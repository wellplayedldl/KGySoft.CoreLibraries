﻿#region Copyright

///////////////////////////////////////////////////////////////////////////////
//  File: ICanUndoInternal.cs
///////////////////////////////////////////////////////////////////////////////
//  Copyright (C) KGy SOFT, 2005-2019 - All Rights Reserved
//
//  You should have received a copy of the LICENSE file at the top-level
//  directory of this distribution. If not, then this file is considered as
//  an illegal copy.
//
//  Unauthorized copying of this file, via any medium is strictly prohibited.
///////////////////////////////////////////////////////////////////////////////

#endregion

namespace KGySoft.ComponentModel
{
    internal interface ICanUndoInternal
    {
        #region Methods

        void SuspendUndo();
        void ResumeUndo();
        void ClearUndoHistory();

        #endregion
    }
}
