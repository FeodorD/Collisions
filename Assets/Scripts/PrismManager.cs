﻿using System.Collections;
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

    private KdTree<Prism> prisms = new KdTree<Prism>(true);
    private KdTree<Prism> filtered = new KdTree<Prism>(true);
    private List<GameObject> prismObjects = new List<GameObject>();
    private GameObject prismParent;
    private Dictionary<Prism, bool> prismColliding = new Dictionary<Prism, bool>();


    private const float UPDATE_RATE = 0.5f;

    #region Unity Functions

    void Start()
    {
        Random.InitState(0);

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

            filtered = prisms;
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
        prisms.UpdatePositions();
        //filtered.UpdatePositions();
        for (int i = 0; i < prisms.Count - 1; i++)
        {

            filtered.RemoveAt(i);
            var nearestObj = filtered.FindClosest(prisms[i].prismObject.transform.position);
            var dist = Vector3.Distance(prisms[i].prismObject.transform.position, nearestObj.transform.position);
            Debug.Log(prismObjects[i].name + " " + nearestObj.name + " DIST: " + dist);
            if (dist <= 1)
            {

                var checkPrisms = new PrismCollision
                {
                    a = prisms[i],
                    b = nearestObj
                };

                yield return checkPrisms;
            }
        }
        yield break;


/*
        for (int i = 0; i < prisms.Count; i++)
            for (int j = i + 1; j < prisms.Count; j++)
            {
                var dist = Vector3.Distance(prismObjects[i].transform.position, prismObjects[j].transform.position);
                Debug.Log(prismObjects[i].name + " " + prismObjects[j].name + " DIST: " + dist);

                if (dist <= 1)
                {

                    var checkPrisms = new PrismCollision
                    {
                        a = prisms[i],
                        b = prisms[j]
                    };

                    yield return checkPrisms;
                }
            }

        yield break;*/

    }

    private bool CheckCollision(PrismCollision collision)
    {
        /*function GJK_intersection(shape p, shape q, vector initial_axis):
            vector A := Support(p, initial_axis) − Support(q, −initial_axis)
            simplex s := { A}
             vector D := −A

           loop:
            A:= Support(p, D) − Support(q, −D)
            if dot(A, D) < 0:
                reject
            s := s ∪ A
            s, D, contains_origin := NearestSimplex(s)
            if contains_origin:
                accept*/

        var prismA = collision.a;
        var prismB = collision.b;
        var centroidA = prismA.points.Aggregate(Vector3.zero, (a, b) => a + b) / prismA.pointCount;
        var centroidB = prismB.points.Aggregate(Vector3.zero, (a, b) => a + b) / prismB.pointCount;

        collision.penetrationDepthVectorAB = new Vector3((Random.value - 0.5f) * 2, 0, (Random.value - 0.5f) * 2);

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

        prismObjA.transform.position += pushA;
        prismObjB.transform.position += pushB;

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
            var prismPoints = prism.points.Select(p => prismTransform.position + Quaternion.AngleAxis(prismTransform.eulerAngles.y, Vector3.up) * new Vector3(p.x * prismTransform.localScale.x, 0, p.z * prismTransform.localScale.z)).ToArray();

            var wireFrameColor = prismColliding[prisms[prismIndex]] ? Color.red : Color.green;

            foreach (var point in prismPoints)
            {
                Debug.DrawLine(point + Vector3.up * yMin, point + Vector3.up * yMax, wireFrameColor);
            }

            for (int i = 0; i < prismPoints.Length; i++)
            {
                Debug.DrawLine(prismPoints[i] + Vector3.up * yMin, prismPoints[(i + 1) % prismPoints.Length] + Vector3.up * yMin, wireFrameColor);
                Debug.DrawLine(prismPoints[i] + Vector3.up * yMax, prismPoints[(i + 1) % prismPoints.Length] + Vector3.up * yMax, wireFrameColor);
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

    #endregion
}
