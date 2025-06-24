﻿using System;

/*───────────────────────────────────────────────────────────────────────────────
 Coordinate2D — serializable replacement for Unity Vector2
 ────────────────────────────────────────────────────────────────────────────────
 Overview
 ════════
 **Coordinate2D** provides a fully serializable 2D coordinate structure that works
 interchangeably with Unity's Vector2. Created to solve binary serialization issues
 where UnityEngine.Vector2 lacks the [Serializable] attribute, causing failures
 with BinaryFormatter during save/load operations.

 Major Responsibilities
 ══════════════════════
 • Binary serialization compatibility for save/load systems
 • Seamless interoperability with Unity Vector2 through implicit conversions
 • Complete mathematical operations (add, subtract, multiply, divide, etc.)
 • Vector mathematics (magnitude, distance, normalization, dot/cross products)
 • Equality comparisons with floating-point tolerance
 • Common vector constants (Zero, One, Up, Down, Left, Right)

 Design Highlights
 ═════════════════
 • **Serializable by Design**: Marked with [Serializable] for BinaryFormatter
 • **Drop-in Replacement**: Implicit conversions allow transparent usage with Vector2
 • **Mathematical Completeness**: Full operator overloading and vector math support
 • **Performance Focused**: Struct design with efficient operations
 • **Unity Integration**: Seamless conversion to/from Vector2 for Unity API calls
 • **Consistent API**: Mirrors Vector2 method names and behavior patterns

 Usage Examples
 ══════════════
   // Direct construction
   var coord = new Coordinate2D(5.0f, 7.0f);
   
   // Implicit conversion from Vector2
   Coordinate2D pos = new Vector2(10, 20);
   
   // Implicit conversion to Vector2
   Vector2 unityPos = coord;
   
   // Mathematical operations
   var result = coord1 + coord2;
   var scaled = coord * 2.5f;
   
   // Vector mathematics
   float distance = Coordinate2D.Distance(coord1, coord2);
   var normalized = coord.normalized;

 Public Interface
 ════════════════
   ── Properties ──────────────────────────────────────────────────────────────
   float x, y                     Component values
   float magnitude                Vector length
   float sqrMagnitude            Squared vector length (faster)
   Coordinate2D normalized       Unit vector in same direction
   
   ── Static Constants ───────────────────────────────────────────────────────
   Zero, One, Up, Down, Left, Right   Common vector directions
   
   ── Mathematical Operations ────────────────────────────────────────────────
   +, -, *, /                    Standard arithmetic operators
   ==, !=                        Equality with floating-point tolerance
   
   ── Vector Mathematics ─────────────────────────────────────────────────────
   Distance(a, b)                Distance between two coordinates
   Dot(a, b)                     Dot product
   Lerp(a, b, t)                 Linear interpolation
   Normalize()                   In-place normalization

 ───────────────────────────────────────────────────────────────────────────────
 MAINTAIN COMPATIBILITY WITH Unity Vector2 API PATTERNS!
 ───────────────────────────────────────────────────────────────────────────── */

namespace HammerAndSickle.Models
{
    /// <summary>
    /// Serializable 2D coordinate structure that provides seamless interoperability 
    /// with Unity Vector2 while supporting binary serialization for save/load operations.
    /// </summary>
    [Serializable]
    public struct Coordinate2D : IEquatable<Coordinate2D>
    {
        #region Constants

        private const float EPSILON = 1e-5f;

        #endregion

        #region Fields

        /// <summary>
        /// X component of the coordinate.
        /// </summary>
        public float x;

        /// <summary>
        /// Y component of the coordinate.
        /// </summary>
        public float y;

        #endregion

        #region Static Properties

        /// <summary>
        /// Coordinate2D with components (0, 0).
        /// </summary>
        public static Coordinate2D Zero => new(0f, 0f);

        /// <summary>
        /// Coordinate2D with components (1, 1).
        /// </summary>
        public static Coordinate2D One => new(1f, 1f);

        /// <summary>
        /// Coordinate2D with components (0, 1).
        /// </summary>
        public static Coordinate2D Up => new(0f, 1f);

        /// <summary>
        /// Coordinate2D with components (0, -1).
        /// </summary>
        public static Coordinate2D Down => new(0f, -1f);

        /// <summary>
        /// Coordinate2D with components (-1, 0).
        /// </summary>
        public static Coordinate2D Left => new(-1f, 0f);

        /// <summary>
        /// Coordinate2D with components (1, 0).
        /// </summary>
        public static Coordinate2D Right => new(1f, 0f);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the magnitude (length) of this coordinate vector.
        /// </summary>
        public float magnitude => MathF.Sqrt(x * x + y * y);

        /// <summary>
        /// Gets the squared magnitude of this coordinate vector.
        /// More efficient than magnitude when you only need to compare lengths.
        /// </summary>
        public float sqrMagnitude => x * x + y * y;

        /// <summary>
        /// Gets a unit vector in the same direction as this coordinate.
        /// Returns Zero if this vector has zero length.
        /// </summary>
        public Coordinate2D normalized
        {
            get
            {
                float mag = magnitude;
                return mag > EPSILON ? new Coordinate2D(x / mag, y / mag) : Zero;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new Coordinate2D with the specified x and y components.
        /// </summary>
        /// <param name="x">The x component</param>
        /// <param name="y">The y component</param>
        public Coordinate2D(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Creates a new Coordinate2D with both components set to the same value.
        /// </summary>
        /// <param name="value">Value for both x and y components</param>
        public Coordinate2D(float value)
        {
            this.x = value;
            this.y = value;
        }

        #endregion

        #region Conversion Operators

        /// <summary>
        /// Implicitly converts a Unity Vector2 to a Coordinate2D.
        /// </summary>
        /// <param name="vector">The Vector2 to convert</param>
        /// <returns>A new Coordinate2D with the same x and y values</returns>
        //public static implicit operator Coordinate2D(Vector2 vector)
        //{
        //    return new Coordinate2D(vector.x, vector.y);
        //}

        /// <summary>
        /// Implicitly converts a Coordinate2D to a Unity Vector2.
        /// </summary>
        /// <param name="coord">The Coordinate2D to convert</param>
        /// <returns>A new Vector2 with the same x and y values</returns>
        //public static implicit operator Vector2(Coordinate2D coord)
        //{
        //    return new Vector2(coord.x, coord.y);
        //}

        #endregion

        #region Arithmetic Operators

        /// <summary>
        /// Adds two coordinates component-wise.
        /// </summary>
        public static Coordinate2D operator +(Coordinate2D a, Coordinate2D b)
        {
            return new Coordinate2D(a.x + b.x, a.y + b.y);
        }

        /// <summary>
        /// Subtracts one coordinate from another component-wise.
        /// </summary>
        public static Coordinate2D operator -(Coordinate2D a, Coordinate2D b)
        {
            return new Coordinate2D(a.x - b.x, a.y - b.y);
        }

        /// <summary>
        /// Negates a coordinate (flips direction).
        /// </summary>
        public static Coordinate2D operator -(Coordinate2D coord)
        {
            return new Coordinate2D(-coord.x, -coord.y);
        }

        /// <summary>
        /// Multiplies a coordinate by a scalar value.
        /// </summary>
        public static Coordinate2D operator *(Coordinate2D coord, float scalar)
        {
            return new Coordinate2D(coord.x * scalar, coord.y * scalar);
        }

        /// <summary>
        /// Multiplies a scalar value by a coordinate.
        /// </summary>
        public static Coordinate2D operator *(float scalar, Coordinate2D coord)
        {
            return new Coordinate2D(coord.x * scalar, coord.y * scalar);
        }

        /// <summary>
        /// Multiplies two coordinates component-wise.
        /// </summary>
        public static Coordinate2D operator *(Coordinate2D a, Coordinate2D b)
        {
            return new Coordinate2D(a.x * b.x, a.y * b.y);
        }

        /// <summary>
        /// Divides a coordinate by a scalar value.
        /// </summary>
        public static Coordinate2D operator /(Coordinate2D coord, float scalar)
        {
            return new Coordinate2D(coord.x / scalar, coord.y / scalar);
        }

        /// <summary>
        /// Divides two coordinates component-wise.
        /// </summary>
        public static Coordinate2D operator /(Coordinate2D a, Coordinate2D b)
        {
            return new Coordinate2D(a.x / b.x, a.y / b.y);
        }

        #endregion

        #region Equality Operators

        /// <summary>
        /// Checks if two coordinates are approximately equal within floating-point tolerance.
        /// </summary>
        public static bool operator ==(Coordinate2D a, Coordinate2D b)
        {
            return MathF.Abs(a.x - b.x) < EPSILON && MathF.Abs(a.y - b.y) < EPSILON;
        }

        /// <summary>
        /// Checks if two coordinates are not approximately equal within floating-point tolerance.
        /// </summary>
        public static bool operator !=(Coordinate2D a, Coordinate2D b)
        {
            return !(a == b);
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// Calculates the distance between two coordinates.
        /// </summary>
        /// <param name="a">First coordinate</param>
        /// <param name="b">Second coordinate</param>
        /// <returns>The distance between the coordinates</returns>
        public static float Distance(Coordinate2D a, Coordinate2D b)
        {
            float deltaX = a.x - b.x;
            float deltaY = a.y - b.y;
            return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        /// <summary>
        /// Calculates the squared distance between two coordinates.
        /// More efficient than Distance when you only need to compare distances.
        /// </summary>
        /// <param name="a">First coordinate</param>
        /// <param name="b">Second coordinate</param>
        /// <returns>The squared distance between the coordinates</returns>
        public static float SqrDistance(Coordinate2D a, Coordinate2D b)
        {
            float deltaX = a.x - b.x;
            float deltaY = a.y - b.y;
            return deltaX * deltaX + deltaY * deltaY;
        }

        /// <summary>
        /// Calculates the dot product of two coordinates.
        /// </summary>
        /// <param name="a">First coordinate</param>
        /// <param name="b">Second coordinate</param>
        /// <returns>The dot product</returns>
        public static float Dot(Coordinate2D a, Coordinate2D b)
        {
            return a.x * b.x + a.y * b.y;
        }

        /// <summary>
        /// Linearly interpolates between two coordinates.
        /// </summary>
        /// <param name="a">Start coordinate</param>
        /// <param name="b">End coordinate</param>
        /// <param name="t">Interpolation factor (0 = a, 1 = b)</param>
        /// <returns>Interpolated coordinate</returns>
        public static Coordinate2D Lerp(Coordinate2D a, Coordinate2D b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new Coordinate2D(
                a.x + (b.x - a.x) * t,
                a.y + (b.y - a.y) * t
            );
        }

        /// <summary>
        /// Linearly interpolates between two coordinates without clamping t.
        /// </summary>
        /// <param name="a">Start coordinate</param>
        /// <param name="b">End coordinate</param>
        /// <param name="t">Interpolation factor</param>
        /// <returns>Interpolated coordinate</returns>
        public static Coordinate2D LerpUnclamped(Coordinate2D a, Coordinate2D b, float t)
        {
            return new Coordinate2D(
                a.x + (b.x - a.x) * t,
                a.y + (b.y - a.y) * t
            );
        }

        /// <summary>
        /// Returns a coordinate with the minimum x and y components from two coordinates.
        /// </summary>
        public static Coordinate2D Min(Coordinate2D a, Coordinate2D b)
        {
            return new Coordinate2D(MathF.Min(a.x, b.x), MathF.Min(a.y, b.y));
        }

        /// <summary>
        /// Returns a coordinate with the maximum x and y components from two coordinates.
        /// </summary>
        public static Coordinate2D Max(Coordinate2D a, Coordinate2D b)
        {
            return new Coordinate2D(MathF.Max(a.x, b.x), MathF.Max(a.y, b.y));
        }

        /// <summary>
        /// Clamps a coordinate to be within the specified minimum and maximum bounds.
        /// </summary>
        public static Coordinate2D Clamp(Coordinate2D value, Coordinate2D min, Coordinate2D max)
        {
            return new Coordinate2D(
                Math.Clamp(value.x, min.x, max.x),
                Math.Clamp(value.y, min.y, max.y)
            );
        }

        #endregion

        #region Instance Methods

        /// <summary>
        /// Normalizes this coordinate to have a magnitude of 1.
        /// If this coordinate has zero length, it remains unchanged.
        /// </summary>
        public void Normalize()
        {
            float mag = magnitude;
            if (mag > EPSILON)
            {
                x /= mag;
                y /= mag;
            }
        }

        /// <summary>
        /// Sets the x and y components of this coordinate.
        /// </summary>
        /// <param name="newX">New x component</param>
        /// <param name="newY">New y component</param>
        public void Set(float newX, float newY)
        {
            x = newX;
            y = newY;
        }

        /// <summary>
        /// Scales this coordinate by the given scale factor.
        /// </summary>
        /// <param name="scale">Scale factor to apply</param>
        public void Scale(float scale)
        {
            x *= scale;
            y *= scale;
        }

        /// <summary>
        /// Scales this coordinate by another coordinate component-wise.
        /// </summary>
        /// <param name="scale">Scale coordinate</param>
        public void Scale(Coordinate2D scale)
        {
            x *= scale.x;
            y *= scale.y;
        }

        #endregion

        #region Object Overrides

        /// <summary>
        /// Determines whether this coordinate equals another coordinate.
        /// </summary>
        /// <param name="other">The coordinate to compare with</param>
        /// <returns>True if the coordinates are approximately equal</returns>
        public bool Equals(Coordinate2D other)
        {
            return this == other;
        }

        /// <summary>
        /// Determines whether this coordinate equals another object.
        /// </summary>
        /// <param name="obj">The object to compare with</param>
        /// <returns>True if the object is a Coordinate2D and approximately equal to this one</returns>
        public override bool Equals(object obj)
        {
            return obj is Coordinate2D other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code for this coordinate.
        /// </summary>
        /// <returns>A hash code</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }

        /// <summary>
        /// Returns a formatted string representation of this coordinate.
        /// </summary>
        /// <returns>String in format "(x, y)"</returns>
        public override string ToString()
        {
            return $"({x:F2}, {y:F2})";
        }

        /// <summary>
        /// Returns a formatted string representation of this coordinate with specified precision.
        /// </summary>
        /// <param name="format">Numeric format string</param>
        /// <returns>Formatted string representation</returns>
        public string ToString(string format)
        {
            return $"({x.ToString(format)}, {y.ToString(format)})";
        }

        #endregion
    }
}