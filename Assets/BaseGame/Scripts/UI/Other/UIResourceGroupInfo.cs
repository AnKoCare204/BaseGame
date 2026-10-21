using System;
using System.Collections.Generic;
using CoreData;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using UnityEngine;
using Core.Manager;
using TW.Utility.Extension;

public class UIResourceGroupInfo : MonoBehaviour
{
    [field: SerializeField] private UIItemInfo UIItemPrefab {get; set;}
    [field: SerializeField] private GameObject EmptyPrefab {get; set;}
    [field: SerializeField] private RectTransform RectTransform {get; set;}
    private readonly List<GameObject> objectList = new List<GameObject>();
    private R3Input R3Input => InputManager.Instance.R3Input;
    private void Start()
    {
        this.UpdateAsObservable()
            .Where(R3Input.WasPressedThisFrame)
            .Subscribe(OnPointerDown)
            .AddTo(this);
    }

    private void OnPointerDown(Unit _)
    {
        Vector3 pointerPosition = R3Input.PointerPosition;
        Debug.Log(pointerPosition);
        if (CameraManager.Instance.RectangleContainsScreenPoint(RectTransform, pointerPosition)) return;
        gameObject.SetActive(false);
    }
    [Button]
    private bool Test()
    {
        Vector3 pointerPosition = R3Input.PointerPosition;
        return CameraManager.Instance.RectangleContainsScreenPoint(RectTransform, pointerPosition);
    } 
    public UIResourceGroupInfo WithResourceList(List<GameResource> resourceList)
    {
        for (int i = 0; i < objectList.Count; i++)
        {
            Destroy(objectList[i]);
        }
        Transform resourceContainer = UIItemPrefab.transform.parent;
        Transform emptyContainer = EmptyPrefab.transform.parent;
        for (int i = 0; i < resourceList.Count; i++)
        {
            GameResource resource = resourceList[i];
            UIItemInfo itemInfo = Instantiate(UIItemPrefab, resourceContainer);
            itemInfo.SetActive(true);
            itemInfo.Init(resource);
            objectList.Add(itemInfo.gameObject);
            GameObject emptyObj = Instantiate(EmptyPrefab, emptyContainer);
            emptyObj.SetActive(true);
            objectList.Add(emptyObj);
        }
        return this;
    }

    public UIResourceGroupInfo WithPosition(Vector3 position)
    {
        transform.position = position;
        return this;
    } 
}