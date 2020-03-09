using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PrismManager : MonoBehaviour
{
    public int prismCount = 10;
    public float prismRegionRadiusXZ = 5;
    public float prismRegionRadiusY = 5;
    public float maxPrismScaleXZ = 5;
    public float maxPrismScaleY = 5;
    public GameObject regularPrismPrefab;
    public GameObject irregularPrismPrefab;

    private List<Prism> prisms = new List<Prism>();
    private List<GameObject> prismObjects = new List<GameObject>();
    private GameObject prismParent;
    private Dictionary<Prism,bool> prismColliding = new Dictionary<Prism, bool>();

    private const float UPDATE_RATE = 0.5f;

    #region Unity Functions

    void Start()
    {
        Random.InitState(0);    //10 for no collision

        prismParent = GameObject.Find("Prisms");
        for (int i = 0; i < prismCount; i++)
        {
            var randPointCount = Mathf.RoundToInt(3 + Random.value * 7);
            var randYRot = Random.value * 360;
            var randScale = new Vector3((Random.value - 0.5f) * 2 * maxPrismScaleXZ, (Random.value - 0.5f) * 2 * maxPrismScaleY, (Random.value - 0.5f) * 2 * maxPrismScaleXZ);
            var randPos = new Vector3((Random.value - 0.5f) * 2 * prismRegionRadiusXZ, (Random.value - 0.5f) * 2 * prismRegionRadiusY, (Random.value - 0.5f) * 2 * prismRegionRadiusXZ);

            GameObject prism = null;
            Prism prismScript = null;
            if (Random.value < 0.5f)
            {
                prism = Instantiate(regularPrismPrefab, randPos, Quaternion.Euler(0, randYRot, 0));
                prismScript = prism.GetComponent<RegularPrism>();
            }
            else
            {
                prism = Instantiate(irregularPrismPrefab, randPos, Quaternion.Euler(0, randYRot, 0));
                prismScript = prism.GetComponent<IrregularPrism>();
            }
            prism.name = "Prism " + i;
            prism.transform.localScale = randScale;
            prism.transform.parent = prismParent.transform;
            prismScript.pointCount = randPointCount;
            prismScript.prismObject = prism;

            prisms.Add(prismScript);
            prismObjects.Add(prism);
            prismColliding.Add(prismScript, false);
        }

        StartCoroutine(Run());
    }
    
    void Update()
    {
        #region Visualization

        DrawPrismRegion();
        DrawPrismWireFrames();

#if UNITY_EDITOR
        if (Application.isFocused)
        {
            UnityEditor.SceneView.FocusWindowIfItsOpen(typeof(UnityEditor.SceneView));
        }
#endif

        #endregion
    }

    IEnumerator Run()
    {
        yield return null;

        while (true)
        {
            foreach (var prism in prisms)
            {
                prismColliding[prism] = false;
            }

            foreach (var collision in PotentialCollisions())
            {
                if (CheckCollision(collision))
                {
                    prismColliding[collision.a] = true;
                    prismColliding[collision.b] = true;

                    ResolveCollision(collision);
                }
            }

            yield return new WaitForSeconds(UPDATE_RATE);
        }
    }

    #endregion

    #region Incomplete Functions

    private IEnumerable<PrismCollision> PotentialCollisions()
    {
        for (int i = 0; i < prisms.Count; i++) {
            for (int j = i + 1; j < prisms.Count; j++) {
                var checkPrisms = new PrismCollision();
                checkPrisms.a = prisms[i];
                checkPrisms.b = prisms[j];

                yield return checkPrisms;
            }
        }

        yield break;
    }

    private bool CheckCollision(PrismCollision collision)
    {
        var prismA = collision.a;
        var prismB = collision.b;

        // simplex 3 vector3 list
        List<Vector3> simplex = new List<Vector3>();

        // starting direction
        Vector3 direction = new Vector3(-1, 0, 0);

        // support function to return furthest point in prism in given direction
        Vector3 support(Prism a, Vector3 d)
        {
            float max = -float.MaxValue;
            int iPt = -1;

            for (int i = 0; i < a.points.Length; i++)
            {
                float dot = Vector3.Dot(d, a.points[i]);
                if (dot > max)
                {
                    max = dot;
                    iPt = i;
                }
            }

            return a.points[iPt];
        }

        // function to return furthest point on minkowski difference convex hull given direction
        Vector3 mink(Prism a, Prism b, Vector3 d)
        {
            Vector3 ptA = support(a, d);
            Vector3 ptB = support(b, -d);

            return (ptA - ptB);
        }

        // Add the first point to the simplex
        simplex.Add(mink(prismA, prismB, direction));

        // Reverse the direction so we get the furthest point in opposite direction
        direction = -direction;

        // Start looping through simplexes with minkowski difference points

        while (true)
        {
            // get new point in the current direction
            Vector3 newPt = mink(prismA, prismB, direction);

            // detect whether or not point goes past the origin, dot product must be positive
            if (Vector3.Dot(newPt, direction) < 0)
            {
                return false;
            }

            // point is past origin so we can add it
            simplex.Add(newPt);

            // check if simplex contains the origin, update if not
            if (contOrigin() == true)
            {
                return true;
            }

        }

        bool contOrigin()
        {
            // if simplex only has 2 points
            if (simplex.Count() == 2)
            {
                // get the points
                Vector3 ptA = simplex[1];
                Vector3 ptB = simplex[0];

                // get the segments
                Vector3 aOrig = new Vector3(0, 0, 0) - ptA;
                Vector3 aB = ptB - ptA;

                // adjust direction to be perp
                direction = new Vector3(-aB[2], 0, aB[0]);

                // if dot product of direction and aOrig less than 0, adjust to point at origin
                if (Vector3.Dot(direction, aOrig) < 0)
                {
                    direction = -direction;
                }

                // keep editing simplex
                return false;
            }
            else if (simplex.Count() == 3)
            {
                Vector3 ptA = simplex[2];
                Vector3 ptB = simplex[1];
                Vector3 ptC = simplex[0];

                // get the segments
                Vector3 aOrig = new Vector3(0, 0, 0) - ptA;
                Vector3 aB = ptB - ptA;
                Vector3 aC = ptC - ptA;

                // adjust direction to be perp to segment away c
                direction = new Vector3(-aB[2], 0, aB[0]);
                if (Vector3.Dot(direction, ptC) > 0)
                {
                    direction = -direction;
                }

                // if perp vector from AB pointing toward origin we can remove c
                if (Vector3.Dot(direction, aOrig) > 0)
                {
                    simplex.Remove(ptC);
                    return false;
                }

                // readjust direction AC from B
                direction = new Vector3(-aC[2], 0, aC[0]);
                if (Vector3.Dot(direction, ptB) > 0)
                {
                    direction = -direction;
                }

                // if perp vector from AC pointing toward origin we can remove b
                if (Vector3.Dot(direction, aOrig) > 0)
                {
                    simplex.Remove(ptB);
                    return false;
                }

                // otherwise the origin is contained inside of the trianglge
                return true;


            }
            else
            {
                return false;
                Debug.Log("Simplex size not equal to 2 or 3");
            }
        }


        collision.penetrationDepthVectorAB = Vector3.zero;

        return true;
    }
    
    #endregion

    #region Private Functions
    
    private void ResolveCollision(PrismCollision collision)
    {
        var prismObjA = collision.a.prismObject;
        var prismObjB = collision.b.prismObject;

        var pushA = -collision.penetrationDepthVectorAB / 2;
        var pushB = collision.penetrationDepthVectorAB / 2;

        for (int i = 0; i < collision.a.pointCount; i++)
        {
            collision.a.points[i] += pushA;
        }
        for (int i = 0; i < collision.b.pointCount; i++)
        {
            collision.b.points[i] += pushB;
        }
        //prismObjA.transform.position += pushA;
        //prismObjB.transform.position += pushB;


        Debug.DrawLine(prismObjA.transform.position, prismObjA.transform.position + collision.penetrationDepthVectorAB, Color.cyan, UPDATE_RATE);
    }
    
    #endregion

    #region Visualization Functions

    private void DrawPrismRegion()
    {
        var points = new Vector3[] { new Vector3(1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1), new Vector3(-1, 0, 1) }.Select(p => p * prismRegionRadiusXZ).ToArray();
        
        var yMin = -prismRegionRadiusY;
        var yMax = prismRegionRadiusY;

        var wireFrameColor = Color.yellow;

        foreach (var point in points)
        {
            Debug.DrawLine(point + Vector3.up * yMin, point + Vector3.up * yMax, wireFrameColor);
        }

        for (int i = 0; i < points.Length; i++)
        {
            Debug.DrawLine(points[i] + Vector3.up * yMin, points[(i + 1) % points.Length] + Vector3.up * yMin, wireFrameColor);
            Debug.DrawLine(points[i] + Vector3.up * yMax, points[(i + 1) % points.Length] + Vector3.up * yMax, wireFrameColor);
        }
    }

    private void DrawPrismWireFrames()
    {
        for (int prismIndex = 0; prismIndex < prisms.Count; prismIndex++)
        {
            var prism = prisms[prismIndex];
            var prismTransform = prismObjects[prismIndex].transform;

            var yMin = prism.midY - prism.height / 2 * prismTransform.localScale.y;
            var yMax = prism.midY + prism.height / 2 * prismTransform.localScale.y;

            var wireFrameColor = prismColliding[prisms[prismIndex]] ? Color.red : Color.green;

            foreach (var point in prism.points)
            {
                Debug.DrawLine(point + Vector3.up * yMin, point + Vector3.up * yMax, wireFrameColor);
            }

            for (int i = 0; i < prism.pointCount; i++)
            {
                Debug.DrawLine(prism.points[i] + Vector3.up * yMin, prism.points[(i + 1) % prism.pointCount] + Vector3.up * yMin, wireFrameColor);
                Debug.DrawLine(prism.points[i] + Vector3.up * yMax, prism.points[(i + 1) % prism.pointCount] + Vector3.up * yMax, wireFrameColor);
            }
        }
    }

    #endregion

    #region Utility Classes

    private class PrismCollision
    {
        public Prism a;
        public Prism b;
        public Vector3 penetrationDepthVectorAB;
    }

    private class Tuple<K,V>
    {
        public K Item1;
        public V Item2;

        public Tuple(K k, V v) {
            Item1 = k;
            Item2 = v;
        }
    }

    #endregion
}
