﻿#region Copyright

///////////////////////////////////////////////////////////////////////////////
//  File: ObjectInitialization.cs
///////////////////////////////////////////////////////////////////////////////
//  Copyright (C) KGy SOFT, 2018 - All Rights Reserved
//
//  You should have received a copy of the LICENSE file at the top-level
//  directory of this distribution. If not, then this file is considered as
//  an illegal copy.
//
//  Unauthorized copying of this file, via any medium is strictly prohibited.
///////////////////////////////////////////////////////////////////////////////

#endregion

namespace KGySoft.Libraries
{
    /// <summary>
    /// Represents a strategy for initializing types when generating random objects.
    /// </summary>
    public enum ObjectInitialization
    {
        /// <summary>
        /// When initializing a new random object the public fields and public read-write properties are set (including non-public setters).
        /// </summary>
        PublicFieldsAndPropeties,

        /// <summary>
        /// When initializing a new random object the public read-write properties (including non-public setters) are set.
        /// </summary>
        PublicProperties,

        /// <summary>
        /// When initializing a new random object fields are set (including non-public and read-only ones). It has a high chance that the object will contain inconsistent data.
        /// </summary>
        Fields
    }
}
