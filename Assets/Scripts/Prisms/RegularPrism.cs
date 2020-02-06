using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RegularPrism : Prism
{
    public bool forwardAxisPerpToFace = true;

    void Start()
    {
        pointCount = Mathf.Max(pointCount, 3);
        points = Enumerable.Range(0, pointCount).Select(i => Quaternion.AngleAxis(360f / pointCount * (i + (forwardAxisPerpToFace ? 0.5f : 0)), Vector3.up) * Vector3.forward * 0.5f).ToArray();
        midY = 0;
        height = 2;
    }
    
}
