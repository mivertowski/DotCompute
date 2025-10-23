
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Algorithms.LinearAlgebra.Operations
{
    /// <summary>
    /// Provides matrix transformation operations (scaling, rotation, reflection, etc.).
    /// </summary>
    public static class MatrixTransforms
    {
        /// <summary>
        /// Creates a scaling transformation matrix.
        /// </summary>
        /// <param name="scaleX">Scale factor for X axis.</param>
        /// <param name="scaleY">Scale factor for Y axis.</param>
        /// <param name="scaleZ">Scale factor for Z axis (optional, for 3D).</param>
        /// <returns>Scaling transformation matrix.</returns>
        public static Matrix CreateScaling(float scaleX, float scaleY, float scaleZ = 1.0f)
        {
            var matrix = new Matrix(4, 4);
            matrix[0, 0] = scaleX;
            matrix[1, 1] = scaleY;
            matrix[2, 2] = scaleZ;
            matrix[3, 3] = 1.0f;
            return matrix;
        }

        /// <summary>
        /// Creates a translation transformation matrix.
        /// </summary>
        /// <param name="translateX">Translation along X axis.</param>
        /// <param name="translateY">Translation along Y axis.</param>
        /// <param name="translateZ">Translation along Z axis (optional, for 3D).</param>
        /// <returns>Translation transformation matrix.</returns>
        public static Matrix CreateTranslation(float translateX, float translateY, float translateZ = 0.0f)
        {
            var matrix = Matrix.Identity(4);
            matrix[0, 3] = translateX;
            matrix[1, 3] = translateY;
            matrix[2, 3] = translateZ;
            return matrix;
        }

        /// <summary>
        /// Creates a rotation transformation matrix around the Z axis.
        /// </summary>
        /// <param name="angleRadians">Rotation angle in radians.</param>
        /// <returns>Rotation transformation matrix.</returns>
        public static Matrix CreateRotationZ(float angleRadians)
        {
            var cos = (float)Math.Cos(angleRadians);
            var sin = (float)Math.Sin(angleRadians);

            var matrix = Matrix.Identity(4);
            matrix[0, 0] = cos;
            matrix[0, 1] = -sin;
            matrix[1, 0] = sin;
            matrix[1, 1] = cos;
            return matrix;
        }

        /// <summary>
        /// Creates a rotation transformation matrix around the X axis.
        /// </summary>
        /// <param name="angleRadians">Rotation angle in radians.</param>
        /// <returns>Rotation transformation matrix.</returns>
        public static Matrix CreateRotationX(float angleRadians)
        {
            var cos = (float)Math.Cos(angleRadians);
            var sin = (float)Math.Sin(angleRadians);

            var matrix = Matrix.Identity(4);
            matrix[1, 1] = cos;
            matrix[1, 2] = -sin;
            matrix[2, 1] = sin;
            matrix[2, 2] = cos;
            return matrix;
        }

        /// <summary>
        /// Creates a rotation transformation matrix around the Y axis.
        /// </summary>
        /// <param name="angleRadians">Rotation angle in radians.</param>
        /// <returns>Rotation transformation matrix.</returns>
        public static Matrix CreateRotationY(float angleRadians)
        {
            var cos = (float)Math.Cos(angleRadians);
            var sin = (float)Math.Sin(angleRadians);

            var matrix = Matrix.Identity(4);
            matrix[0, 0] = cos;
            matrix[0, 2] = sin;
            matrix[2, 0] = -sin;
            matrix[2, 2] = cos;
            return matrix;
        }

        /// <summary>
        /// Creates a rotation transformation matrix around an arbitrary axis.
        /// </summary>
        /// <param name="axis">Rotation axis (must be unit vector).</param>
        /// <param name="angleRadians">Rotation angle in radians.</param>
        /// <returns>Rotation transformation matrix.</returns>
        public static Matrix CreateRotationAroundAxis(Matrix axis, float angleRadians)
        {
            ArgumentNullException.ThrowIfNull(axis);

            if (axis.Rows != 3 || axis.Columns != 1)
            {
                throw new ArgumentException("Axis must be a 3x1 column vector.");
            }

            var x = axis[0, 0];
            var y = axis[1, 0];
            var z = axis[2, 0];

            var cos = (float)Math.Cos(angleRadians);
            var sin = (float)Math.Sin(angleRadians);
            var oneMinusCos = 1.0f - cos;

            var matrix = new Matrix(4, 4);

            // Rodrigues' rotation formula in matrix form
            matrix[0, 0] = cos + x * x * oneMinusCos;
            matrix[0, 1] = x * y * oneMinusCos - z * sin;
            matrix[0, 2] = x * z * oneMinusCos + y * sin;
            matrix[0, 3] = 0;

            matrix[1, 0] = y * x * oneMinusCos + z * sin;
            matrix[1, 1] = cos + y * y * oneMinusCos;
            matrix[1, 2] = y * z * oneMinusCos - x * sin;
            matrix[1, 3] = 0;

            matrix[2, 0] = z * x * oneMinusCos - y * sin;
            matrix[2, 1] = z * y * oneMinusCos + x * sin;
            matrix[2, 2] = cos + z * z * oneMinusCos;
            matrix[2, 3] = 0;

            matrix[3, 0] = 0;
            matrix[3, 1] = 0;
            matrix[3, 2] = 0;
            matrix[3, 3] = 1;

            return matrix;
        }

        /// <summary>
        /// Creates a reflection transformation matrix across a plane.
        /// </summary>
        /// <param name="planeNormal">Normal vector of the reflection plane (must be unit vector).</param>
        /// <returns>Reflection transformation matrix.</returns>
        public static Matrix CreateReflection(Matrix planeNormal)
        {
            ArgumentNullException.ThrowIfNull(planeNormal);

            if (planeNormal.Rows != 3 || planeNormal.Columns != 1)
            {
                throw new ArgumentException("Plane normal must be a 3x1 column vector.");
            }

            var nx = planeNormal[0, 0];
            var ny = planeNormal[1, 0];
            var nz = planeNormal[2, 0];

            var matrix = new Matrix(4, 4);

            // Reflection matrix: I - 2 * n * n^T
            matrix[0, 0] = 1 - 2 * nx * nx;
            matrix[0, 1] = -2 * nx * ny;
            matrix[0, 2] = -2 * nx * nz;
            matrix[0, 3] = 0;

            matrix[1, 0] = -2 * ny * nx;
            matrix[1, 1] = 1 - 2 * ny * ny;
            matrix[1, 2] = -2 * ny * nz;
            matrix[1, 3] = 0;

            matrix[2, 0] = -2 * nz * nx;
            matrix[2, 1] = -2 * nz * ny;
            matrix[2, 2] = 1 - 2 * nz * nz;
            matrix[2, 3] = 0;

            matrix[3, 0] = 0;
            matrix[3, 1] = 0;
            matrix[3, 2] = 0;
            matrix[3, 3] = 1;

            return matrix;
        }

        /// <summary>
        /// Creates a shear transformation matrix.
        /// </summary>
        /// <param name="shearXY">Shear factor for X in Y direction.</param>
        /// <param name="shearXZ">Shear factor for X in Z direction.</param>
        /// <param name="shearYX">Shear factor for Y in X direction.</param>
        /// <param name="shearYZ">Shear factor for Y in Z direction.</param>
        /// <param name="shearZX">Shear factor for Z in X direction.</param>
        /// <param name="shearZY">Shear factor for Z in Y direction.</param>
        /// <returns>Shear transformation matrix.</returns>
        public static Matrix CreateShear(float shearXY = 0, float shearXZ = 0, float shearYX = 0,
                                       float shearYZ = 0, float shearZX = 0, float shearZY = 0)
        {
            var matrix = Matrix.Identity(4);
            matrix[0, 1] = shearXY;
            matrix[0, 2] = shearXZ;
            matrix[1, 0] = shearYX;
            matrix[1, 2] = shearYZ;
            matrix[2, 0] = shearZX;
            matrix[2, 1] = shearZY;
            return matrix;
        }

        /// <summary>
        /// Creates a perspective projection transformation matrix.
        /// </summary>
        /// <param name="fieldOfViewRadians">Field of view angle in radians.</param>
        /// <param name="aspectRatio">Aspect ratio (width/height).</param>
        /// <param name="nearPlane">Near clipping plane distance.</param>
        /// <param name="farPlane">Far clipping plane distance.</param>
        /// <returns>Perspective projection matrix.</returns>
        public static Matrix CreatePerspective(float fieldOfViewRadians, float aspectRatio, float nearPlane, float farPlane)
        {
            var tanHalfFov = (float)Math.Tan(fieldOfViewRadians * 0.5f);
            var range = nearPlane - farPlane;

            var matrix = new Matrix(4, 4);
            matrix[0, 0] = 1.0f / (aspectRatio * tanHalfFov);
            matrix[1, 1] = 1.0f / tanHalfFov;
            matrix[2, 2] = (farPlane + nearPlane) / range;
            matrix[2, 3] = 2.0f * farPlane * nearPlane / range;
            matrix[3, 2] = -1.0f;

            return matrix;
        }

        /// <summary>
        /// Creates an orthographic projection transformation matrix.
        /// </summary>
        /// <param name="left">Left clipping plane.</param>
        /// <param name="right">Right clipping plane.</param>
        /// <param name="bottom">Bottom clipping plane.</param>
        /// <param name="top">Top clipping plane.</param>
        /// <param name="nearPlane">Near clipping plane.</param>
        /// <param name="farPlane">Far clipping plane.</param>
        /// <returns>Orthographic projection matrix.</returns>
        public static Matrix CreateOrthographic(float left, float right, float bottom, float top, float nearPlane, float farPlane)
        {
            var matrix = new Matrix(4, 4);

            var width = right - left;
            var height = top - bottom;
            var depth = farPlane - nearPlane;

            matrix[0, 0] = 2.0f / width;
            matrix[0, 3] = -(right + left) / width;

            matrix[1, 1] = 2.0f / height;
            matrix[1, 3] = -(top + bottom) / height;

            matrix[2, 2] = -2.0f / depth;
            matrix[2, 3] = -(farPlane + nearPlane) / depth;

            matrix[3, 3] = 1.0f;

            return matrix;
        }

        /// <summary>
        /// Creates a look-at view transformation matrix.
        /// </summary>
        /// <param name="eye">Eye position (camera position).</param>
        /// <param name="target">Target position (look-at point).</param>
        /// <param name="up">Up vector.</param>
        /// <returns>Look-at view transformation matrix.</returns>
        public static Matrix CreateLookAt(Matrix eye, Matrix target, Matrix up)
        {
            ArgumentNullException.ThrowIfNull(eye);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(up);

            if (eye.Rows != 3 || eye.Columns != 1 ||
                target.Rows != 3 || target.Columns != 1 ||
                up.Rows != 3 || up.Columns != 1)
            {
                throw new ArgumentException("Eye, target, and up vectors must be 3x1 column vectors.");
            }

            // Compute forward vector (normalized)
            var forward = new Matrix(3, 1);
            forward[0, 0] = target[0, 0] - eye[0, 0];
            forward[1, 0] = target[1, 0] - eye[1, 0];
            forward[2, 0] = target[2, 0] - eye[2, 0];
            forward = NormalizeVector(forward);

            // Compute right vector (forward x up, normalized)
            var right = CrossProduct(forward, up);
            right = NormalizeVector(right);

            // Compute true up vector (right x forward)
            var trueUp = CrossProduct(right, forward);

            // Create view matrix
            var matrix = new Matrix(4, 4);

            // Right vector
            matrix[0, 0] = right[0, 0];
            matrix[1, 0] = right[1, 0];
            matrix[2, 0] = right[2, 0];

            // Up vector
            matrix[0, 1] = trueUp[0, 0];
            matrix[1, 1] = trueUp[1, 0];
            matrix[2, 1] = trueUp[2, 0];

            // Forward vector (negated for right-handed system)
            matrix[0, 2] = -forward[0, 0];
            matrix[1, 2] = -forward[1, 0];
            matrix[2, 2] = -forward[2, 0];

            // Translation
            matrix[0, 3] = -DotProduct(right, eye);
            matrix[1, 3] = -DotProduct(trueUp, eye);
            matrix[2, 3] = DotProduct(forward, eye);

            matrix[3, 3] = 1.0f;

            return matrix;
        }

        /// <summary>
        /// Applies a transformation to a point or vector.
        /// </summary>
        /// <param name="transformation">Transformation matrix.</param>
        /// <param name="point">Point or vector to transform.</param>
        /// <param name="isPoint">True if transforming a point, false for a vector.</param>
        /// <param name="accelerator">Optional compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Transformed point or vector.</returns>
        public static async Task<Matrix> ApplyTransformAsync(Matrix transformation, Matrix point, bool isPoint = true, IAccelerator? accelerator = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(transformation);
            ArgumentNullException.ThrowIfNull(point);

            // Convert to homogeneous coordinates if needed
            Matrix homogeneousPoint;
            if (point.Rows == 3 && point.Columns == 1)
            {
                homogeneousPoint = new Matrix(4, 1);
                homogeneousPoint[0, 0] = point[0, 0];
                homogeneousPoint[1, 0] = point[1, 0];
                homogeneousPoint[2, 0] = point[2, 0];
                homogeneousPoint[3, 0] = isPoint ? 1.0f : 0.0f;
            }
            else if (point.Rows == 4 && point.Columns == 1)
            {
                homogeneousPoint = point;
            }
            else
            {
                throw new ArgumentException("Point must be a 3x1 or 4x1 column vector.");
            }

            // Apply transformation
            Matrix result;
            if (accelerator != null)
            {
                result = await MatrixOperations.MultiplyAsync(transformation, homogeneousPoint, accelerator, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                result = new Matrix(transformation.Rows, homogeneousPoint.Columns);
                for (var i = 0; i < transformation.Rows; i++)
                {
                    for (var j = 0; j < homogeneousPoint.Columns; j++)
                    {
                        float sum = 0;
                        for (var k = 0; k < transformation.Columns; k++)
                        {
                            sum += transformation[i, k] * homogeneousPoint[k, j];
                        }
                        result[i, j] = sum;
                    }
                }
            }

            // Convert back to 3D if needed and perform perspective divide for points
            if (result.Rows == 4 && isPoint && Math.Abs(result[3, 0]) > 1e-10f)
            {
                var converted = new Matrix(3, 1);
                var w = result[3, 0];
                converted[0, 0] = result[0, 0] / w;
                converted[1, 0] = result[1, 0] / w;
                converted[2, 0] = result[2, 0] / w;
                return converted;
            }

            return result;
        }

        // Helper methods
        private static Matrix NormalizeVector(Matrix vector)
        {
            var norm = MatrixStatistics.VectorNorm(vector, 2.0f);
            if (Math.Abs(norm) < 1e-10f)
            {

                throw new InvalidOperationException("Cannot normalize zero vector.");
            }


            var normalized = new Matrix(vector.Rows, vector.Columns);
            for (var i = 0; i < vector.Rows; i++)
            {
                normalized[i, 0] = vector[i, 0] / norm;
            }
            return normalized;
        }

        private static Matrix CrossProduct(Matrix a, Matrix b)
        {
            if (a.Rows != 3 || a.Columns != 1 || b.Rows != 3 || b.Columns != 1)
            {

                throw new ArgumentException("Cross product requires 3x1 vectors.");
            }


            var result = new Matrix(3, 1);
            result[0, 0] = a[1, 0] * b[2, 0] - a[2, 0] * b[1, 0];
            result[1, 0] = a[2, 0] * b[0, 0] - a[0, 0] * b[2, 0];
            result[2, 0] = a[0, 0] * b[1, 0] - a[1, 0] * b[0, 0];
            return result;
        }

        private static float DotProduct(Matrix a, Matrix b)
        {
            if (a.Rows != b.Rows || a.Columns != 1 || b.Columns != 1)
            {

                throw new ArgumentException("Dot product requires vectors of same length.");
            }


            float result = 0;
            for (var i = 0; i < a.Rows; i++)
            {
                result += a[i, 0] * b[i, 0];
            }
            return result;
        }
    }
}