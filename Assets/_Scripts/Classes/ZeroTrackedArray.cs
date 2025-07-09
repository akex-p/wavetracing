using System;
using UnityEngine;

// Super cool Class to check wheter IR is empty or not... efficiently!
public class ZeroTrackedArray
{
    private double[] _array;
    private bool _isZeroed;

    public ZeroTrackedArray(int size)
    {
        _array = new double[size];
        _isZeroed = true; // Initially zeroed
    }

    public double this[int index]
    {
        get => _array[index];
        set
        {
            if (value != 0f && _isZeroed)
            {
                _isZeroed = false;
            }
            _array[index] = value;
        }
    }

    public bool IsZeroed() => _isZeroed;

    public void Clear()
    {
        Array.Clear(_array, 0, _array.Length);
        _isZeroed = true;
    }

    public double[] ToDoubleArray() => _array; // use with caution -> direct reference (!!!)

    public float[] ToFloatArray()
    {
        float[] floatArray = new float[_array.Length];
        for (int i = 0; i < _array.Length; i++)
        {
            floatArray[i] = (float)_array[i]; // Explicitly cast double to float
        }
        return floatArray;
    }
}