using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct SplineSegment2D
{
    //4 Unknowns:
    public float a_x, b_x, c_x, d_x, a_y, b_y, c_y, d_y;

    //Domain:
    public float tStart, tEnd;
}

public class ParametricSpline2D : MonoBehaviour
{
    [Header("Spline Configuration")]

    [Header("Data Nodes: ")]
    public List<Vector2> nodes = new List<Vector2>();

    [Header("Spline Type: ")]
    public bool isCubicSpline = true;
    [Space]
    public bool isNaturalSpline = true;

    public float leftFirstDerivativeX = 0f;
    public float leftFirstDerivativeY = 0f;

    public float rightFirstDerivativeX = 0f;
    public float rightFirstDerivativeY = 0f;

    [Header("Computed Functions: ")]
    public List<SplineSegment2D> functions = new List<SplineSegment2D>();

    [Header("Visualization: ")]
    public bool drawGizmos = true;
    [Range(10, 100)] public int resolution = 50;
    [Space]
    public bool showTangents = false;
    public bool showAccelerations = false;
    [Space]
    public float vectorScale = 0.2f;
    [Space]
    public Color nodeColor;
    public Color splineColor;
    public Color velocityColor;
    public Color accelerationColor;

    private void OnValidate()
    {
        //Ensure At Least 2 Points Exist:
        if (nodes != null && nodes.Count >= 2)
        {
            //Recalculate Spline:
            CalculateSpline();
        }
    }

    public void CalculateSpline()
    {
        if (isCubicSpline)
        {
            CubicSplineCalc();
        }
        else
        {
            QuadraticSplineCalc();
        }
    }

    private void QuadraticSplineCalc()
    {
        int n = nodes.Count;
        if (n < 2) return;

        functions.Clear();
        int numberOfFunctions = n - 1;
        int totalUnknowns = 3 * numberOfFunctions;

        //A MATRIX (Coefficient Matrix):
        float[,] variableMatrix = new float[totalUnknowns, totalUnknowns];

        //B VECTOR (Constant Matrix):
        float[] constantMatrixX = new float[totalUnknowns];
        float[] constantMatrixY = new float[totalUnknowns];

        int currentRow = 0;

        //1. INTERPOLATION CONDITIONS
        for (int i = 0; i < numberOfFunctions; i++)
        {
            //Each Function Contains 3 Unknowns:
            int colBase = 3 * i;


            //Left Domain:
            float tLeft = i;

            //*(FILL ROW IN A MATRIX)*
            //  [(a_1) * x_left^2]
            variableMatrix[currentRow, colBase + 0] = Mathf.Pow(tLeft, 2f);
            //  [(b_1) * x_left]
            variableMatrix[currentRow, colBase + 1] = tLeft;
            //  [(c_1) * 1]
            variableMatrix[currentRow, colBase + 2] = 1f;

            //*(FILL ROW IN B MATRIX)*
            constantMatrixX[currentRow] = nodes[i].x;
            constantMatrixY[currentRow] = nodes[i].y;
            currentRow++;


            //Right Domain:
            float tRight = i + 1;

            //*(FILL ROW IN A MATRIX)*
            //  [(a_1) * x_right^2]
            variableMatrix[currentRow, colBase + 0] = Mathf.Pow(tRight, 2f);
            //  [(b_1) * x_right]
            variableMatrix[currentRow, colBase + 1] = tRight;
            //  [y_right]
            variableMatrix[currentRow, colBase + 2] = 1f;

            //*(FILL ROW IN B MATRIX)*
            constantMatrixX[currentRow] = nodes[i + 1].x;
            constantMatrixY[currentRow] = nodes[i + 1].y;
            currentRow++;
        }

        //2. FIRST DERIVATIVE CONTINUITY: P'_i(x) = P'_i+1(x) 
        for (int i = 0; i < numberOfFunctions - 1; i++)
        {
            float sharedT = i + 1;

            int leftCol = 3 * i;
            //  [2 * (a_1) * x]
            variableMatrix[currentRow, leftCol + 0] = 2f * sharedT;
            //  [1 * (b_1)]
            variableMatrix[currentRow, leftCol + 1] = 1f;
            //  [0 * (c_1)]
            variableMatrix[currentRow, leftCol + 2] = 0f;

            int rightCol = 3 * (i + 1);
            //  [2 * (a_2) * x]
            variableMatrix[currentRow, rightCol + 0] = -2f * sharedT;
            //  [1 * (b_2)]
            variableMatrix[currentRow, rightCol + 1] = -1f;
            //  [0 * (c_2)]
            variableMatrix[currentRow, rightCol + 2] = 0f;

            constantMatrixX[currentRow] = 0f;
            constantMatrixY[currentRow] = 0f;
            currentRow++;
        }

        //3. BOUNDARY CONDITION / ANCHOR: Set first function's quadratic coefficient a_0 = 0
        variableMatrix[currentRow, 0] = 1f;
        constantMatrixX[currentRow] = 0f;
        constantMatrixY[currentRow] = 0f;
        currentRow++;

        //4. SOLVE MATRIX SYSTEM USING GAUSSIAN ELIMINATION
        float[] solvedCoefficientsX = GaussElimination(variableMatrix, constantMatrixX);
        float[] solvedCoefficientsY = GaussElimination(variableMatrix, constantMatrixY);
        if (solvedCoefficientsX == null || solvedCoefficientsY == null)
        {
            Debug.LogError("Matrix resolution failed!");
            return;
        }

        //5. PLUG IN UNKNOWNS INTO THE FUNCTIONS
        for (int i = 0; i < numberOfFunctions; i++)
        {
            int colBase = 3 * i;
            SplineSegment2D function = new SplineSegment2D
            {
                tStart = i,
                tEnd = i + 1,

                a_x = 0f,
                b_x = solvedCoefficientsX[colBase + 0],
                c_x = solvedCoefficientsX[colBase + 1],
                d_x = solvedCoefficientsX[colBase + 2],

                a_y = 0f,
                b_y = solvedCoefficientsY[colBase + 0],
                c_y = solvedCoefficientsY[colBase + 1],
                d_y = solvedCoefficientsY[colBase + 2]
            };
            functions.Add(function);
        }
    }

    public void CubicSplineCalc()
    {
        int n = nodes.Count;
        if (n < 2) return;

        functions.Clear();
        int numberOfFunctions = n - 1;
        int totalUnknowns = 4 * numberOfFunctions;

        //A MATRIX (Coefficient Matrix):
        float[,] variableMatrix = new float[totalUnknowns, totalUnknowns];

        //B VECTOR (Constant Matrix):
        float[] constantMatrixX = new float[totalUnknowns];
        float[] constantMatrixY = new float[totalUnknowns];

        int currentRow = 0;

        //1. INTERPOLATION CONDITIONS
        for (int i = 0; i < numberOfFunctions; i++)
        {
            //Each Function Contains 4 Unknowns:
            int colBase = 4 * i;

            //Left Domain:
            float tLeft = i;

            //*(FILL ROW IN A MATRIX)*
            //  [(a_1) * x_left^3]
            variableMatrix[currentRow, colBase + 0] = Mathf.Pow(tLeft, 3f);
            //  [(b_1) * x_left^2]
            variableMatrix[currentRow, colBase + 1] = Mathf.Pow(tLeft, 2f);
            //  [(c_1) * x_left]
            variableMatrix[currentRow, colBase + 2] = tLeft;
            //  [(d_1) * 1]
            variableMatrix[currentRow, colBase + 3] = 1f;

            //*(FILL ROW IN B MATRIX)*
            constantMatrixX[currentRow] = nodes[i].x;
            constantMatrixY[currentRow] = nodes[i].y;
            currentRow++;

            //Right Domain:
            float tRight = i + 1;

            //*(FILL ROW IN A MATRIX)*
            //  [(a_1) * x_right^3]
            variableMatrix[currentRow, colBase + 0] = Mathf.Pow(tRight, 3f);
            //  [(b_1) * x_right^2]
            variableMatrix[currentRow, colBase + 1] = Mathf.Pow(tRight, 2f);
            //  [(c_1) * x_right]
            variableMatrix[currentRow, colBase + 2] = tRight;
            //  [(d_1) * 1]
            variableMatrix[currentRow, colBase + 3] = 1f;

            //*(FILL ROW IN B MATRIX)*
            //  [y_right]
            constantMatrixX[currentRow] = nodes[i + 1].x;
            constantMatrixY[currentRow] = nodes[i + 1].y;
            currentRow++;
        }

        //2. FIRST DERIVATIVE CONTINUITY: P'_i(x) = P'_i+1(x) 
        for (int i = 0; i < numberOfFunctions - 1; i++)
        {
            float sharedT = i + 1;

            int leftCol = 4 * i;

            //  [3 * (a_1) * x^2]
            variableMatrix[currentRow, leftCol + 0] = 3f * Mathf.Pow(sharedT, 2f);
            //  [2 * (b_1) * x]
            variableMatrix[currentRow, leftCol + 1] = 2f * sharedT;
            //  [1 * (c_1)]
            variableMatrix[currentRow, leftCol + 2] = 1f;
            //  [0 * (d_1)]
            variableMatrix[currentRow, leftCol + 3] = 0f;

            int rightCol = 4 * (i + 1);

            //  [3 * (a_2) * x^2]
            variableMatrix[currentRow, rightCol + 0] = -3f * Mathf.Pow(sharedT, 2f);
            //  [2 * (b_2) * x]
            variableMatrix[currentRow, rightCol + 1] = -2f * sharedT;
            //  [1 * (c_2)]
            variableMatrix[currentRow, rightCol + 2] = -1f;
            //  [0 * (d_2)]
            variableMatrix[currentRow, rightCol + 3] = 0f;

            constantMatrixX[currentRow] = 0f;
            constantMatrixY[currentRow] = 0f;
            currentRow++;
        }

        //3. SECOND DERIVATIVE CONTINUITY: P''_i(x) = P''_i+1(x) 
        for (int i = 0; i < numberOfFunctions - 1; i++)
        {
            float sharedT = i + 1;

            int leftCol = 4 * i;

            //  [6 * (a_1) * x]
            variableMatrix[currentRow, leftCol + 0] = 6f * sharedT;
            //  [2 * (b_1)]
            variableMatrix[currentRow, leftCol + 1] = 2f;
            //  [0 * (c_1)]
            variableMatrix[currentRow, leftCol + 2] = 0f;
            //  [0 * (d_1)]
            variableMatrix[currentRow, leftCol + 3] = 0f;

            int rightCol = 4 * (i + 1);

            //  [6 * (a_2) * x]
            variableMatrix[currentRow, rightCol + 0] = -6f * sharedT;
            //  [2 * (b_2)]
            variableMatrix[currentRow, rightCol + 1] = -2f;
            //  [0 * (c_2)]
            variableMatrix[currentRow, rightCol + 2] = 0f;
            //  [0 * (d_2)]
            variableMatrix[currentRow, rightCol + 3] = 0f;

            constantMatrixX[currentRow] = 0f;
            constantMatrixY[currentRow] = 0f;
            currentRow++;
        }

        //4. BOUNDARY CONDITIONS (Natural vs Clamped)
        float leftT = 0f;
        float rightT = numberOfFunctions;

        if (isNaturalSpline)
        {
            //Left Natural: P_0''(leftX) = 0
            variableMatrix[currentRow, 0] = 6f * leftT;
            variableMatrix[currentRow, 1] = 2f;
            constantMatrixX[currentRow] = 0f;
            constantMatrixY[currentRow] = 0f;
            currentRow++;

            int lastCol = 4 * (numberOfFunctions - 1);

            //Right Natural: P_n-1''(rightX) = 0
            variableMatrix[currentRow, lastCol + 0] = 6f * rightT;
            variableMatrix[currentRow, lastCol + 1] = 2f;
            constantMatrixX[currentRow] = 0f;
            constantMatrixY[currentRow] = 0f;
            currentRow++;
        }
        else
        {
            //Left Clamped: P_0'(leftX) = leftFirstDerivative
            variableMatrix[currentRow, 0] = 3f * Mathf.Pow(leftT, 2f);
            variableMatrix[currentRow, 1] = 2f * leftT;
            variableMatrix[currentRow, 2] = 1f;
            constantMatrixX[currentRow] = leftFirstDerivativeX;
            constantMatrixY[currentRow] = leftFirstDerivativeY;
            currentRow++;

            int lastCol = 4 * (numberOfFunctions - 1);

            //Right Clamped: P_n-1'(rightX) = rightFirstDerivative
            variableMatrix[currentRow, lastCol + 0] = 3f * Mathf.Pow(rightT, 2f);
            variableMatrix[currentRow, lastCol + 1] = 2f * rightT;
            variableMatrix[currentRow, lastCol + 2] = 1f;
            constantMatrixX[currentRow] = rightFirstDerivativeX;
            constantMatrixY[currentRow] = rightFirstDerivativeY;
            currentRow++;
        }

        //5. SOLVE MATRIX SYSTEM USING GAUSSIAN ELIMINATION
        float[] solvedCoefficientsX = GaussElimination(variableMatrix, constantMatrixX);
        float[] solvedCoefficientsY = GaussElimination(variableMatrix, constantMatrixY);
        if (solvedCoefficientsX == null || solvedCoefficientsY == null)
        {
            Debug.LogError("Matrix resolution failed!");
            return;
        }

        //6. PLUG IN UNKNOWNS INTO THE FUNCTIONS
        for (int i = 0; i < numberOfFunctions; i++)
        {
            int colBase = 4 * i;
            SplineSegment2D function = new SplineSegment2D
            {
                tStart = i,
                tEnd = i + 1,

                a_x = solvedCoefficientsX[colBase + 0],
                b_x = solvedCoefficientsX[colBase + 1],
                c_x = solvedCoefficientsX[colBase + 2],
                d_x = solvedCoefficientsX[colBase + 3],

                a_y = solvedCoefficientsY[colBase + 0],
                b_y = solvedCoefficientsY[colBase + 1],
                c_y = solvedCoefficientsY[colBase + 2],
                d_y = solvedCoefficientsY[colBase + 3]
            };

            functions.Add(function);
        }
    }

    /// <summary>
    /// Solves system of linear equations A * X = B using Gaussian elimination with partial pivoting.
    /// </summary>
    private float[] GaussElimination(float[,] A, float[] B)
    {
        int n = B.Length;
        float[] X = new float[n];
        float[,] workA = (float[,])A.Clone();
        float[] workB = (float[])B.Clone();

        for (int p = 0; p < n; p++)
        {
            int maxRow = p;
            for (int i = p + 1; i < n; i++)
            {
                if (Mathf.Abs(workA[i, p]) > Mathf.Abs(workA[maxRow, p]))
                {
                    maxRow = i;
                }
            }

            for (int k = p; k < n; k++)
            {
                float temp = workA[p, k];
                workA[p, k] = workA[maxRow, k];
                workA[maxRow, k] = temp;
            }

            float tempB = workB[p];
            workB[p] = workB[maxRow];
            workB[maxRow] = tempB;

            if (Mathf.Abs(workA[p, p]) < 1e-9f) return null;

            for (int i = p + 1; i < n; i++)
            {
                float alpha = workA[i, p] / workA[p, p];
                workB[i] -= alpha * workB[p];
                for (int k = p; k < n; k++)
                {
                    workA[i, k] -= alpha * workA[p, k];
                }
            }
        }

        for (int i = n - 1; i >= 0; i--)
        {
            float sum = 0f;
            for (int k = i + 1; k < n; k++)
            {
                sum += workA[i, k] * X[k];
            }
            X[i] = (workB[i] - sum) / workA[i, i];
        }

        return X;
    }

    public SplineSegment2D GetFunctionForT(float t)
    {
        if (functions.Count == 0) return default;

        for (int i = 0; i < functions.Count; i++)
        {
            if (t >= functions[i].tStart && t <= functions[i].tEnd)
                return functions[i];
        }

        return t < functions[0].tStart ? functions[0] : functions[functions.Count - 1];
    }

    public Vector2 Evaluate(float t)
    {
        SplineSegment2D f = GetFunctionForT(t);
        float x = f.a_x * Mathf.Pow(t, 3f) + f.b_x * Mathf.Pow(t, 2f) + f.c_x * t + f.d_x;
        float y = f.a_y * Mathf.Pow(t, 3f) + f.b_y * Mathf.Pow(t, 2f) + f.c_y * t + f.d_y;

        return new Vector2(x, y);
    }

    public Vector2 EvaluateFirstDerivative(float t)
    {
        SplineSegment2D f = GetFunctionForT(t);
        float dx = 3f * f.a_x * t * t + 2f * f.b_x * t + f.c_x;
        float dy = 3f * f.a_y * t * t + 2f * f.b_y * t + f.c_y;

        return new Vector2(dx, dy);
    }

    public Vector2 EvaluateSecondDerivative(float t)
    {
        SplineSegment2D f = GetFunctionForT(t);
        float ddx = 6f * f.a_x * t + 2f * f.b_x;
        float ddy = 6f * f.a_y * t + 2f * f.b_y;

        return new Vector2(ddx, ddy);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || nodes == null || nodes.Count < 2 || functions.Count == 0) return;

        // Draw Spline Curve
        Gizmos.color = splineColor;
        for (int i = 0; i < functions.Count; i++)
        {
            //Define Domain and Step Increment For Current Function:
            float tStart = functions[i].tStart;
            float tEnd = functions[i].tEnd;
            float step = (tEnd - tStart) / resolution;

            //Define P(x) At Start Of Domain As Initial Position:
            Vector2 prevPt = Evaluate(tStart);

            for (int j = 1; j <= resolution; j++)
            {
                float t = tStart + j * step;
                Vector2 currentPt = Evaluate(t);
                Gizmos.DrawLine(prevPt, currentPt);

                //Draw Velocity Vectors (First Derivative):
                if (showTangents)
                {
                    Vector2 derivative = EvaluateFirstDerivative(t);
                    Vector2 tangentDir = derivative.normalized;

                    Gizmos.color = velocityColor;
                    Gizmos.DrawLine(currentPt, currentPt + tangentDir * vectorScale);

                    Gizmos.color = splineColor;
                }

                //Draw Acceleration Vectors (Second Derivative):
                if (showAccelerations)
                {
                    Vector2 acceleration = EvaluateSecondDerivative(t);

                    Gizmos.color = accelerationColor;
                    Gizmos.DrawLine(currentPt, currentPt + acceleration * vectorScale);

                    Gizmos.color = splineColor;
                }

                prevPt = currentPt;
            }
        }

        //Draw Node Point:
        Gizmos.color = nodeColor;
        foreach (var p in nodes)
        {
            Gizmos.DrawSphere(new Vector3(p.x, p.y, 0f), 0.15f);
        }
    }
}
