using UnityEngine;
using UnityEngine.EventSystems;

namespace LumiWorld.Acs
{
    // A panel surface is interactive for EconomyHUD's map/camera guard even between buttons.
    public sealed class ResourceUIInputBlocker : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public void OnPointerDown(PointerEventData eventData) { }
        public void OnPointerClick(PointerEventData eventData) { }
    }
}
