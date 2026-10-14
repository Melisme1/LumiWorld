using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LumiWorld.Acs
{
    // Optional binding for a future avatar list/detail view. No prefabs or art are required.
    [DisallowMultipleComponent]
    public sealed class CreatureCareUIBridge : MonoBehaviour
    {
        [SerializeField] private ResourceProductionRuntime production;
        [SerializeField] private ResourceInventoryUIController mapSelection;
        [SerializeField] private bool followMapSelection = true;
        [Min(.1f)] [SerializeField] private float refreshSeconds = .25f;
        [SerializeField] private string pantryFoodId = "resource.leafwood.raspberries";
        [Min(1)] [SerializeField] private int pantryTransferAmount = 50;
        [SerializeField] private UnityEvent onViewChanged = new UnityEvent();
        [SerializeField] private UnityEvent<string> onActionMessage = new UnityEvent<string>();
        private ResourceProductionRuntime subscribedProduction;
        private ResourceInventoryUIController subscribedSelection;
        private float nextRefresh;
        public string HabitatId { get; private set; }
        public string IndividualId { get; private set; }
        public IReadOnlyList<CreatureResidentView> Residents { get; private set; } = Array.Empty<CreatureResidentView>();
        public CreatureResidentView SelectedCreature { get; private set; }
        public CreatureFeedPreview FeedPreview { get; private set; }
        public IReadOnlyList<CreatureFoodStock> FoodStocks { get; private set; } = Array.Empty<CreatureFoodStock>();

        private void OnEnable() { Connect(); Refresh(); }
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + Mathf.Max(.1f, refreshSeconds);
            Connect();
            if (followMapSelection && mapSelection != null && HabitatId != mapSelection.SelectedHabitatId)
                SelectHabitat(mapSelection.SelectedHabitatId);
            else Refresh();
        }
        private void Connect()
        {
            if (production == null) production = FindAnyObjectByType<ResourceProductionRuntime>();
            if (mapSelection == null && followMapSelection) mapSelection = FindAnyObjectByType<ResourceInventoryUIController>();
            if (subscribedProduction != production)
            {
                if (subscribedProduction != null) subscribedProduction.ResidentsChanged -= Refresh;
                subscribedProduction = production;
                if (subscribedProduction != null) subscribedProduction.ResidentsChanged += Refresh;
            }
            var selection = followMapSelection ? mapSelection : null;
            if (subscribedSelection != selection)
            {
                if (subscribedSelection != null) subscribedSelection.HabitatSelected -= SelectHabitat;
                subscribedSelection = selection;
                if (subscribedSelection != null) subscribedSelection.HabitatSelected += SelectHabitat;
            }
        }
        public void SelectHabitat(string habitatId)
        {
            if (HabitatId != habitatId) IndividualId = null;
            HabitatId = habitatId; Refresh();
        }
        public void SelectCreature(string individualId)
        {
            IndividualId = null;
            foreach (var resident in Residents) if (resident.IndividualId == individualId) { IndividualId = individualId; break; }
            Refresh();
        }
        public void FeedSelected()
        {
            string message = "Chọn một cư dân trước.";
            if (production != null && !string.IsNullOrEmpty(IndividualId)) production.TryFeed(HabitatId, IndividualId, out message);
            try { onActionMessage.Invoke(message); } catch (Exception exception) { Debug.LogException(exception, this); }
            Refresh();
        }
        public void SetPantryFood(string resourceId) => pantryFoodId = resourceId;
        public void SetPantryTransferAmount(int amount) => pantryTransferAmount = Math.Max(1, amount);
        public void DepositSelectedFood()
        {
            string message = "Chọn một cư dân trước.";
            if (production != null && !string.IsNullOrEmpty(IndividualId))
                production.DepositCreatureFood(HabitatId, IndividualId, pantryFoodId, pantryTransferAmount, out message);
            PublishMessage(message); Refresh();
        }
        public void WithdrawSelectedFood()
        {
            string message = "Chọn một cư dân trước.";
            if (production != null && !string.IsNullOrEmpty(IndividualId))
                production.WithdrawCreatureFood(IndividualId, pantryFoodId, pantryTransferAmount, out message);
            PublishMessage(message); Refresh();
        }
        private void PublishMessage(string message)
        { try { onActionMessage.Invoke(message); } catch (Exception exception) { Debug.LogException(exception, this); } }
        public void Refresh()
        {
            Residents = production != null ? production.GetResidents(HabitatId) : Array.Empty<CreatureResidentView>();
            SelectedCreature = null;
            foreach (var resident in Residents) if (resident.IndividualId == IndividualId) { SelectedCreature = resident; break; }
            if (SelectedCreature == null) IndividualId = null;
            FoodStocks = SelectedCreature != null ? production.GetCreatureFoodStocks(IndividualId) : Array.Empty<CreatureFoodStock>();
            FeedPreview = SelectedCreature != null ? production.PreviewFeed(HabitatId, IndividualId) : null;
            try { onViewChanged.Invoke(); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
        private void OnDisable()
        {
            if (subscribedProduction != null) subscribedProduction.ResidentsChanged -= Refresh;
            if (subscribedSelection != null) subscribedSelection.HabitatSelected -= SelectHabitat;
            subscribedProduction = null; subscribedSelection = null;
        }
    }
}
