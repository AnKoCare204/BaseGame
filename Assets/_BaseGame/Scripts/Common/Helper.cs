using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
namespace BanThanh
{
    public static class Helper
    {
        #region List
        public static T GetRandomElementFromList<T>(List<T> list)
        {
            return list[Random.Range(0, list.Count)];
        }

        public static List<T> GetRandomListWithUniqueElements<T>(List<T> list, int numberOfElements)
        {
            List<T> result = new();
            List<T> tempList = new(list);
            numberOfElements = Mathf.Min(numberOfElements, tempList.Count);
            while (result.Count < numberOfElements)
            {
                int randomIndex = Random.Range(0, tempList.Count);
                T randomElement = tempList[randomIndex];
                result.Add(randomElement);
                tempList.RemoveAt(randomIndex);
            }
            return result;
        }

        public static List<T> GetNewListFromListWithEndIndex<T>(List<T> list, int index)
        {
            index++;
            List<T> result = new();
            if (index > list.Count) index = list.Count;
            for (int i = 0; i < index; i++)
            {
                result.Add(list[i]);
            }
            return result;
        }
        #endregion

        #region Vector3
        public static Vector3 GetDirection(Transform a, Transform b)
        {
            return (b.position - a.position).normalized;
        }

        public static bool CheckDistance(Vector3 start, Vector3 end, float threshold)
        {
            return Vector3.Distance(start, end) <= threshold;
        }

        public static Vector3 GetRandomPoint(List<Transform> transforms, float randomRange1, float randomRange2)
        {
            Vector3 position = transforms[Random.Range(0, transforms.Count)].position;
            Vector3 addition = new(Random.Range(randomRange1, randomRange2), 0f, Random.Range(randomRange1, randomRange2));
            return position + addition;
        }

        public static Vector3 GetDirection(Vector3 a, Vector3 b)
        {
            return (b - a).normalized;
        }

        public static Vector3 GetSpecificDirection(Transform a, Transform b)
        {
            Vector3 position = b.position;
            return new Vector3(position.x - a.position.x, 0, position.z - a.position.z).normalized;
        }

        public static string GetMinutesAndSecondTimeFormat(TimeSpan timeSpan)
        {
            return string.Format("{0}:{1:00}", timeSpan.Minutes.ToString(), timeSpan.Seconds.ToString());
        }

        public static string GetCurrencyFormat<T>(T value)
        {
            return double.Parse(value.ToString()).ToString("#,###", CultureInfo.GetCultureInfo("vi-VN").NumberFormat);
        }

        public static Quaternion GetQuaternion(Vector3 targetPosition, Transform transform)
        {
            Vector3 position = transform.position;
            Vector3 lookAtPosition = new(targetPosition.x - position.x,
                        position.y,
                        targetPosition.z - position.z);
            return Quaternion.LookRotation(lookAtPosition);
        }

        public static void ClampValue(ref int value, int min, int max)
        {
            if (value > max)
            {
                value = min;
            }
        }

        public static Vector3 GetSpecificDirection(Vector3 a, Vector3 b)
        {
            return new Vector3(b.x - a.x, 0, b.z - a.z).normalized;
        }

        public static Vector3 GetRandomVector(Vector3 origin, float minRange, float maxRange)
        {
            return origin + new Vector3(Random.Range(minRange, maxRange), origin.y, Random.Range(minRange, maxRange));
        }

        public static Vector3 GetRandomVector(Vector3 origin, float maxRange)
        {
            return origin + new Vector3(Random.Range(0, maxRange), origin.y, Random.Range(0, maxRange));
        }

        public static Vector3 GetRandomVectorXAxis(Vector3 origin, float maxRange, float maxZ)
        {
            return origin + new Vector3(Random.Range(0, maxRange), origin.y, maxZ);
        }

        public static Vector3 GetRandomVector(Vector3 origin, Vector3 range)
        {
            return origin + new Vector3(Random.Range(-range.x, range.x), 0f, Random.Range(0, range.z));
        }

        public static Vector3 GetRandomPosition(Vector3 origin, float minRange, float maxRange)
        {
            return origin + new Vector3(Random.Range(minRange, maxRange), Random.Range(minRange, maxRange));
        }
        #endregion

        #region Object
        public static Quaternion RandomRotation()
        {
            Vector3 rotation = new(0f, Random.Range(0, 360), 0);
            return Quaternion.Euler(rotation);
        }

        public static Transform GetObjectTransform<T>(T obj)
        {
            return (obj as MonoBehaviour)?.GetComponent<Transform>();
        }

        public static RectTransform GetObjectRectTransform<T>(T obj)
        {
            return (obj as MonoBehaviour)?.GetComponent<RectTransform>();
        }

        public static List<Transform> GetObjectTransforms<T>(List<T> list)
        {
            List<Transform> transforms = new();
            for (int i = 0; i < list.Count; i++)
            {
                Transform cachedTransform = (list[i] as MonoBehaviour).GetComponent<Transform>();
                transforms.Add(cachedTransform);
            }
            return transforms;
        }

        public static Transform GetRandomTransform(List<Transform> transforms)
        {
            int randomIndex = Random.Range(0, transforms.Count);
            return transforms[randomIndex];
        }


        public static Vector3 GetObjectPosition<T>(T obj)
        {
            return (obj as MonoBehaviour).GetComponent<Transform>().position;
        }

        public static List<Vector3> GetObjectPositions<T>(List<T> list)
        {
            List<Vector3> positions = new();
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 position = (list[i] as MonoBehaviour).GetComponent<Transform>().position;
                positions.Add(position);
            }
            return positions;
        }

        public static List<Quaternion> GetObjectRotations<T>(List<T> list)
        {
            List<Quaternion> quaternions = new();
            for (int i = 0; i < list.Count; i++)
            {
                Quaternion quaternion = (list[i] as MonoBehaviour).GetComponent<Transform>().rotation;
                quaternions.Add(quaternion);
            }
            return quaternions;
        }

        public static void SetObjectPosition(MonoBehaviour monoObject, Vector3 position)
        {
            monoObject.transform.position = position;
        }

        public static void SetObjectPosition(MonoBehaviour monoObject, Transform transform, float height)
        {
            monoObject.transform.position = new Vector3(transform.position.x,
                    transform.position.y + height,
                    transform.position.z);
        }

        public static void SetObjectPositions<T>(List<T> objects, List<Transform> points, float height)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                Transform objTransform = (objects[i] as MonoBehaviour).transform;
                objTransform.position = new Vector3(points[i].position.x,
                    points[i].position.y + height,
                    points[i].position.z);
            }
        }

        public static void SetObjectRandomPositions<T>(List<T> objects, List<Transform> points, float height)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                Transform randomTransform = GetRandomTransform(points);
                Transform objTransform = (objects[i] as MonoBehaviour).transform;
                objTransform.position = new Vector3(randomTransform.position.x,
                    randomTransform.position.y + height,
                    randomTransform.position.z);
                points.Remove(randomTransform);
            }
        }

        public static void SetObjectPositions(List<Transform> objectTransforms, List<Transform> points, float height)
        {
            for (int i = 0; i < objectTransforms.Count; i++)
            {
                objectTransforms[i].position = new Vector3(points[i].position.x,
                    points[i].position.y + height,
                    points[i].position.z);
            }
        }

        public static void SetObjectRotations(List<Transform> objectTransforms, List<Quaternion> quaternions)
        {
            for (int i = 0; i < objectTransforms.Count; i++)
            {
                objectTransforms[i].rotation = quaternions[i];
            }
        }
        #endregion

        #region Color
        public static string ColorToHex(Color32 color)
        {
            string hex = color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
            return hex;
        }

        public static void ChangeColorAlpha(Color color1, Color color2, float alpha)
        {
            Color tempColor = color1;
            tempColor.a = alpha;
            color2 = tempColor;
        }

        public static Color HexToColor(string hex)
        {
            hex = hex.Replace("0x", ""); //in case the string is formatted 0xFFFFFF
            hex = hex.Replace("#", ""); //in case the string is formatted #FFFFFF
            byte a = 255; //assume fully visible unless specified in hex
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            //Only use alpha if the string has enough characters
            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return new Color32(r, g, b, a);
        }

        public static Color HexToColorWithAlpha(string hex, byte alpha)
        {
            hex = hex.Replace("0x", ""); //in case the string is formatted 0xFFFFFF
            hex = hex.Replace("#", ""); //in case the string is formatted #FFFFFF
            byte a = alpha; //assume fully visible unless specified in hex
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            //Only use alpha if the string has enough characters
            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return new Color32(r, g, b, a);
        }

        public static Color RGBToFloat(Vector3 rgb)
        {
            float r = rgb.x / 255;
            float b = rgb.y / 255;
            float g = rgb.z / 255;
            return new Color(r, g, b);
        }
        #endregion

        #region Extensions
        public static void AddElements<T>(this List<T> list, params T[] elements)
        {
            list.AddRange(elements);
        }

        public static void SetActive(this Image image, bool active)
        {
            if (image.gameObject.activeSelf == active)
                return;
            image.gameObject.SetActive(active);
        }

        public static void SetActive(this Button button, bool active)
        {
            if (button.gameObject.activeSelf == active)
                return;
            button.gameObject.SetActive(active);
        }

        public static void SetActive(this RectTransform rectTransform, bool active)
        {
            if (rectTransform.gameObject.activeSelf == active)
                return;
            rectTransform.gameObject.SetActive(active);
        }

        public static void SetActive(this TextMeshProUGUI text, bool active)
        {
            if (text.gameObject.activeSelf == active)
                return;
            text.gameObject.SetActive(active);
        }

        #endregion

        #region Angle
        public static float GetAngleFromTwoPositions(Transform from, Transform to)
        {
            if (from == null || to == null)
            {
                return 0f;
            }
            float xDistance = to.position.x - from.position.x;
            float zDistance = to.position.z - from.position.z;
            float angle = (Mathf.Atan2(zDistance, xDistance) * Mathf.Rad2Deg) - 90f;
            angle = GetNormalizedAngle(angle);
            return angle;
        }

        public static void SetEulerAnglesZAxis(this Transform self, float z)
        {
            Vector3 selfAngles = self.eulerAngles;
            self.rotation = Quaternion.Euler(selfAngles.x, selfAngles.y, z);
        }

        public static void SetRotationFromTwoTransforms(this Transform self, Transform from, Transform to)
        {
            Vector3 direction = from.position - to.position;
            self.rotation = Quaternion.LookRotation(direction);
        }

        public static float GetNormalizedAngle(float angle)
        {
            while (angle < 0f)
            {
                angle += 360f;
            }
            while (360f < angle)
            {
                angle -= 360f;
            }
            return angle;
        }
        #endregion

        #region Time
        public static double GetTimeBetweenDays(int days)
        {
            DateTime currentDate = DateTime.Now;
            DateTime nextDay = currentDate.Date.AddDays(days);
            TimeSpan timeRemaining = nextDay - currentDate;
            return timeRemaining.TotalSeconds;
        }

        public static bool IsDaysBetweenDate(DateTime date01, DateTime date02, double days)
        {
            TimeSpan timeDifference = date02 - date01;
            TimeSpan dayBettween = TimeSpan.FromDays(days);
            return (timeDifference > dayBettween);
        }
        #endregion

        #region String
        public static string[] SplitString(string input, string splitter)
        {
            return input.Split(new string[] { splitter }, System.StringSplitOptions.None);
        }

        public static string ConvertTime(TimeSpan timeSpan, float time)
        {
            timeSpan = TimeSpan.FromSeconds(time);
            return string.Format("{0:D2}h:{1:D2}m:{2:D2}s",
                   timeSpan.Hours.ToString(), timeSpan.Minutes.ToString(), timeSpan.Seconds.ToString());
        }

        public static string CapitalizeFirstCharacter(string input)
        {
            string capitalizedText = char.ToUpper(input[0]) + input[1..].ToLower();
            return capitalizedText;
        }

        public static string ProperNameStandardization(string name)
        {
            TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
            return textInfo.ToTitleCase(name.ToLower());
        }

        public static string ConvertPascalCaseEnumToString<T>(T enumValue) where T : Enum
        {
            string enumString = enumValue.ToString();
            string displayString = Regex.Replace(enumString, "([a-z])([A-Z])", "$1 $2");
            return displayString;
        }

        public static string RemoveString(string origin, string removedString)
        {
            if (string.IsNullOrEmpty(origin)) return "";
            return string.IsNullOrEmpty(removedString) ? origin : origin.Replace(removedString, "");
        }

        public static Vector3 StringToVector3(string str)
        {
            Vector3 vector = Vector3.zero;

            return vector;
        }
        #endregion

        #region Enums
        public static T GetRandomValueFromEnum<T>() where T : Enum
        {
            T[] enumValues = GetEnumList<T>();
            T randomEnumValue = enumValues[Random.Range(0, enumValues.Length)];
            return randomEnumValue;
        }
        public static T GetRandomValueFromEnum<T>(int minInclusive) where T : Enum
        {
            T[] enumValues = GetEnumList<T>();
            T randomEnumValue = enumValues[Random.Range(minInclusive, enumValues.Length)];
            return randomEnumValue;
        }

        public static T GetRandomValueFromEnum<T>(int minInclusive, int maxExClusive) where T : Enum
        {
            T[] enumValues = GetEnumList<T>();
            T randomEnumValue = enumValues[Random.Range(minInclusive, maxExClusive)];
            return randomEnumValue;
        }

        public static T[] GetEnumList<T>() where T : Enum
        {
            return (T[])Enum.GetValues(typeof(T));
        }

        public static int GetEnumLength<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Length;
        }
        #endregion

        #region Array, List
        public static void ShuffleList<T>(List<T> array)
        {
            System.Random random = new();
            int n = array.Count;

            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);

                (array[j], array[i]) = (array[i], array[j]);
            }
        }
        #endregion

    }
}