﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.InteropServices.JavaScript
{
    public partial class JSObject
    {
        public bool IsDisposed { get => _isDisposed; }
        internal IntPtr JSHandle;

        internal GCHandle? InFlight;
        internal int InFlightCounter;
        private bool _isDisposed;

        internal JSObject(IntPtr jsHandle)
        {
            JSHandle = jsHandle;
            InFlight = null;
            InFlightCounter = 0;
        }

        internal void AddInFlight()
        {
            if (IsDisposed) throw new ObjectDisposedException($"Cannot access a disposed {GetType().Name}.");
            lock (this)
            {
                InFlightCounter++;
                if (InFlightCounter == 1)
                {
                    Debug.Assert(InFlight == null, "InFlight == null");
                    InFlight = GCHandle.Alloc(this, GCHandleType.Normal);
                }
            }
        }

        // Note that we could not use SafeHandle.DangerousAddRef() and DangerousRelease()
        // because we could get to zero InFlightCounter multiple times across lifetime of the JSObject
        // we only want JSObject to be disposed (from GC finalizer) once there is no in-flight reference and also no natural C# reference
        internal void ReleaseInFlight()
        {
            lock (this)
            {
                Debug.Assert(InFlightCounter != 0, "InFlightCounter != 0");

                InFlightCounter--;
                if (InFlightCounter == 0)
                {
                    Debug.Assert(InFlight.HasValue, "InFlight.HasValue");
                    InFlight.Value.Free();
                    InFlight = null;
                }
            }
        }

        public override bool Equals([NotNullWhen(true)] object? obj) => obj is JSObject other && JSHandle == other.JSHandle;

        public override int GetHashCode() => (int)JSHandle;

        public override string ToString() => $"(js-obj js '{JSHandle}')";

        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                JSHostImplementation.ReleaseCSOwnedObject(JSHandle);
                _isDisposed = true;
                JSHandle = IntPtr.Zero;
            }
        }

        ~JSObject()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
