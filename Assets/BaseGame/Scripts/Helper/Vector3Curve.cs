using UnityEngine;

[System.Serializable]
public class Vector3Curve
{
    public AnimationCurve XCurve;
    public AnimationCurve YCurve;
    public AnimationCurve ZCurve;
    private Vector3 temp;
    public Vector3 Evaluate(Vector3 start, Vector3 end, float time)
    {
        temp.x = Mathf.LerpUnclamped(start.x, end.x, XCurve.Evaluate(time));
        temp.y = Mathf.LerpUnclamped(start.y, end.y, YCurve.Evaluate(time));
        temp.z = Mathf.LerpUnclamped(start.z, end.z, ZCurve.Evaluate(time));
        return temp;
    }
        
}