using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/calendar")]
    [AddComponentMenu("Evo/UI/UI Elements/Calendar")]
    public class Calendar : MonoBehaviour
    {
        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private bool highlightToday = true;
        [SerializeField, Range(0, 100)] private int yearRangeFromCurrent = 50;

        [EvoHeader("Initial Date", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private InitialDateMode initialDateMode = InitialDateMode.Today;
        [SerializeField] private int customYear = 1999;
        [SerializeField, Range(0, 12)] private int customMonth = 6;
        [SerializeField, Range(0, 31)] private int customDay = 21;

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private TMP_Text dateLabel;
        [SerializeField] private Dropdown monthDropdown;
        [SerializeField] private Dropdown yearDropdown;
        [SerializeField] private Button previousMonthButton;
        [SerializeField] private Button nextMonthButton;
        [SerializeField] private Transform daysContainer;
        [SerializeField] private GameObject dayButtonPrefab;

        [EvoHeader("Month Data", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private MonthData january = new("January");
        [SerializeField] private MonthData february = new("February");
        [SerializeField] private MonthData march = new("March");
        [SerializeField] private MonthData april = new("April");
        [SerializeField] private MonthData may = new("May");
        [SerializeField] private MonthData june = new("June");
        [SerializeField] private MonthData july = new("July");
        [SerializeField] private MonthData august = new("August");
        [SerializeField] private MonthData september = new("September");
        [SerializeField] private MonthData october = new("October");
        [SerializeField] private MonthData november = new("November");
        [SerializeField] private MonthData december = new("December");

        MonthData[] Months => new MonthData[]
        {
            january, february, march, april, may, june, july, august, september, october, november, december
        };

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CUSTOM_EDITOR_ID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public UnityEvent<DateTime> onDateSelected = new();

        // Helpers
        bool isInitialized;
        int startYear;
        int yearCount;
        DateTime minDate = new(1900, 1, 1);
        DateTime maxDate = new(2100, 12, 31);
        DateTime currentDisplayMonth = DateTime.Now;
        DateTime? selectedDate;
        readonly List<CalendarDay> dayButtons = new();

        public enum InitialDateMode
        {
            None,
            Today,
            Custom
        }

        [Serializable]
        public class MonthData
        {
            [Tooltip("Display name for the month.")]
            public string monthName;
#if EVO_LOCALIZATION
            [Tooltip("Localization key for Evo Localization integration.")]
            public string localizationKey;
#endif
            public MonthData(string name) { monthName = name; }
            public string GetDisplayName() { return monthName; }
        }

        void Start()
        {
            Initialize();

#if EVO_LOCALIZATION
            if (enableLocalization)
            {
                localizedObject = Localization.LocalizedObject.Check(gameObject);
                if (localizedObject != null)
                {
                    Localization.LocalizationManager.OnLanguageSet += UpdateLocalization;
                    UpdateLocalization();
                }
            }
#endif
        }

        void Initialize()
        {
            if (isInitialized)
                return;

            switch (initialDateMode)
            {
                case InitialDateMode.Today:
                    selectedDate = DateTime.Now.Date;
                    currentDisplayMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    break;

                case InitialDateMode.Custom:
                    try
                    {
                        int maxDay = DateTime.DaysInMonth(customYear, customMonth);
                        selectedDate = new DateTime(customYear, customMonth, Math.Min(customDay, maxDay));
                        currentDisplayMonth = new DateTime(selectedDate.Value.Year, selectedDate.Value.Month, 1);
                    }
                    catch
                    {
                        selectedDate = DateTime.Now.Date;
                        currentDisplayMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    }
                    break;

                case InitialDateMode.None:
                    selectedDate = null;
                    currentDisplayMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    break;
            }

            // Set year count
            yearCount = Math.Min(maxDate.Year, DateTime.Now.Year + yearRangeFromCurrent);

            // Setup dropdowns
            if (monthDropdown != null) { SetupMonthDropdown(); }
            if (yearDropdown != null) { SetupYearDropdown(); }

            // Setup navigation buttons
            if (previousMonthButton != null) { previousMonthButton.onClick.AddListener(PreviousMonth); }
            if (nextMonthButton != null) { nextMonthButton.onClick.AddListener(NextMonth); }

            // Create day buttons
            CreateDayButtons();

            // Initial display
            UpdateDisplay();

            // Fire initial selection event if date is pre-selected
            if (selectedDate.HasValue) { onDateSelected?.Invoke(selectedDate.Value); }

            // Set as init'd
            isInitialized = true;
        }

        void SetupMonthDropdown()
        {
            if (monthDropdown == null)
                return;

            monthDropdown.ClearAllItems();

            List<string> monthNames = new();
            foreach (var month in Months) { monthNames.Add(month.GetDisplayName()); }

            monthDropdown.AddItems(monthNames.ToArray());
            monthDropdown.onItemSelected.AddListener(monthIndex =>
            {
                currentDisplayMonth = new DateTime(currentDisplayMonth.Year, monthIndex + 1, 1);
                UpdateDisplay();
            });
        }

        void SetupYearDropdown()
        {
            if (yearDropdown == null)
                return;

            yearDropdown.ClearAllItems();

            // Calculate year range
            int currentYear = DateTime.Now.Year;
            startYear = Math.Max(minDate.Year, currentYear - yearRangeFromCurrent);
            int endYear = Math.Min(maxDate.Year, currentYear + yearRangeFromCurrent);

            List<string> years = new();
            for (int year = startYear; year <= endYear; year++) { years.Add(year.ToString()); }

            yearDropdown.AddItems(years.ToArray());
            yearDropdown.onItemSelected.AddListener(yearIndex =>
            {
                int selectedYear = startYear + yearIndex;
                currentDisplayMonth = new DateTime(selectedYear, currentDisplayMonth.Month, 1);
                UpdateDisplay();
            });
        }

        void CreateDayButtons()
        {
            // Create 42 buttons (6 weeks x 7 days) to cover all possible month layouts
            foreach (Transform obj in daysContainer) { Destroy(obj.gameObject); }
            for (int i = 0; i < 42; i++)
            {
                GameObject buttonObj = Instantiate(dayButtonPrefab, daysContainer);
                CalendarDay dButton = buttonObj.GetComponent<CalendarDay>();
                dayButtons.Add(dButton);

                int dayIndex = i; // Capture for closure
                dButton.button.onClick.AddListener(() => OnDayClicked(dayIndex));
            }
        }

        void UpdateDisplay()
        {
            // Update dropdowns without triggering events
            if (monthDropdown != null) { monthDropdown.SelectItem(currentDisplayMonth.Month - 1, false); }

            // Update year dropdown
            if (yearDropdown != null)
            {
                int yearIndex = currentDisplayMonth.Year - startYear;
                if (yearIndex >= 0 && yearIndex < yearDropdown.items.Count) { yearDropdown.SelectItem(yearIndex, false); }
            }

            // Update date label
            if (dateLabel != null) { dateLabel.text = $"{Months[currentDisplayMonth.Month - 1].GetDisplayName()} {currentDisplayMonth.Year}"; }

            // Update navigation button states
            UpdateNavigationButtons();

            // Get first day of month and days in month
            DateTime firstDay = new(currentDisplayMonth.Year, currentDisplayMonth.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(currentDisplayMonth.Year, currentDisplayMonth.Month);
            int startDayOfWeek = (int)firstDay.DayOfWeek;

            // Get days from previous month
            DateTime previousMonth = currentDisplayMonth.AddMonths(-1);
            int daysInPreviousMonth = DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month);

            // Update all day buttons
            for (int i = 0; i < dayButtons.Count; i++)
            {
                CalendarDay dButton = dayButtons[i];
                DateTime buttonDate;
                bool isCurrentMonth = false;

                if (i < startDayOfWeek)
                {
                    // Previous month days
                    int day = daysInPreviousMonth - (startDayOfWeek - i - 1);
                    buttonDate = new DateTime(previousMonth.Year, previousMonth.Month, day);
                    dButton.SetLabel(day.ToString());
                }

                else if (i < startDayOfWeek + daysInMonth)
                {
                    // Current month days
                    int day = i - startDayOfWeek + 1;
                    buttonDate = new DateTime(currentDisplayMonth.Year, currentDisplayMonth.Month, day);
                    dButton.SetLabel(day.ToString());
                    isCurrentMonth = true;
                }

                else
                {
                    // Next month days
                    int day = i - startDayOfWeek - daysInMonth + 1;
                    DateTime nextMonth = currentDisplayMonth.AddMonths(1);
                    buttonDate = new DateTime(nextMonth.Year, nextMonth.Month, day);
                    dButton.SetLabel(day.ToString());
                }

                // Store date in button for click handling
                dButton.gameObject.name = buttonDate.ToString("yyyy-MM-dd");

                // Update button appearance
                UpdateDayAppearance(dButton, buttonDate, isCurrentMonth);
            }
        }

        void UpdateDayAppearance(CalendarDay dayButton, DateTime date, bool isCurrentMonth)
        {
            // Check if date is within allowed range
            bool isEnabled = date >= minDate && date <= maxDate;
            dayButton.button.SetInteractable(isEnabled);

            // Apply styling based on state
            if (selectedDate.HasValue && date.Date == selectedDate.Value.Date) { dayButton.SetState(3); } // Selected date
            else if (highlightToday && date.Date == DateTime.Now.Date) { dayButton.SetState(2); } // Today's date
            else if (!isCurrentMonth) { dayButton.SetState(0); } // Days from other months
            else { dayButton.SetState(1); } // Normal
        }

        void OnDayClicked(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= dayButtons.Count)
                return;

            CalendarDay dButton = dayButtons[buttonIndex];
            if (!dButton.button.interactable) { return; }

            // Parse date from button name
            if (DateTime.TryParse(dButton.button.gameObject.name, out DateTime clickedDate)) { SelectDate(clickedDate); }
        }

        void PreviousMonth()
        {
            DateTime newMonth = currentDisplayMonth.AddMonths(-1);

            // Check if the new month is within the year dropdown range
            // Don't allow navigation outside dropdown range
            if (newMonth.Year < startYear)
                return;

            // Also check against absolute min date
            if (new DateTime(newMonth.Year, newMonth.Month, DateTime.DaysInMonth(newMonth.Year, newMonth.Month)) < minDate)
                return;

            currentDisplayMonth = newMonth;
            UpdateDisplay();
        }

        void NextMonth()
        {
            DateTime newMonth = currentDisplayMonth.AddMonths(1);

            // Get the end year from dropdown
            int endYear = startYear + yearCount;

            // Check if the new month is within the year dropdown range
            // Don't allow navigation outside dropdown range
            if (newMonth.Year > endYear)
                return;

            // Also check against absolute max date
            if (new DateTime(newMonth.Year, newMonth.Month, 1) > maxDate)
                return;

            currentDisplayMonth = newMonth;
            UpdateDisplay();
        }

        void UpdateNavigationButtons()
        {
            if (previousMonthButton != null && nextMonthButton != null)
            {
                DateTime prevMonth = currentDisplayMonth.AddMonths(-1);
                DateTime nextMonth = currentDisplayMonth.AddMonths(1);

                // Get the year range
                int endYear = startYear + yearCount;

                // Check both year dropdown range and absolute date limits
                bool canGoPrevious = prevMonth.Year >= startYear &&
                        new DateTime(prevMonth.Year, prevMonth.Month, DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month)) >= minDate;
                bool canGoNext = nextMonth.Year <= endYear &&
                        new DateTime(nextMonth.Year, nextMonth.Month, 1) <= maxDate;

                previousMonthButton.interactable = canGoPrevious;
                nextMonthButton.interactable = canGoNext;
            }
        }

        public DateTime? GetSelectedDate()
        {
            return selectedDate;
        }

        public void SelectDate(DateTime date, bool invokeEvents = true)
        {
            if (!isInitialized) { Initialize(); }
            selectedDate = date;

            // Update display to show the selected date's month
            currentDisplayMonth = new DateTime(date.Year, date.Month, 1);
            UpdateDisplay();

            // Invoke event
            if (invokeEvents) { onDateSelected?.Invoke(date); }
        }

        public void SetMinDate(DateTime date)
        {
            if (!isInitialized) { Initialize(); }
            minDate = date;

            SetupYearDropdown(); // Refresh year dropdown
            UpdateDisplay();
        }

        public void SetMaxDate(DateTime date)
        {
            if (!isInitialized) { Initialize(); }
            maxDate = date;

            SetupYearDropdown(); // Refresh year dropdown
            UpdateDisplay();
        }

        public void ClearSelection()
        {
            selectedDate = null;
            UpdateDisplay();
        }

        public void GoToToday()
        {
            currentDisplayMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            UpdateDisplay();
        }

        public void SelectToday()
        {
            SelectDate(DateTime.Now.Date);
        }

        public void SetYearRange(int range)
        {
            yearRangeFromCurrent = range;
            SetupYearDropdown();
            UpdateDisplay();
        }

#if EVO_LOCALIZATION
        void OnDestroy()
        {
            if (enableLocalization && localizedObject != null)
            {
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
            }
        }

        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            foreach (MonthData item in Months)
            {
                if (!string.IsNullOrEmpty(item.localizationKey))
                {
                    item.monthName = localizedObject.GetString(item.localizationKey);
                }
            }

            if (monthDropdown != null) { SetupMonthDropdown(); }
            UpdateDisplay();
        }
#endif

#if UNITY_EDITOR
        [NonSerialized] public bool settingsFoldout = true;
        [NonSerialized] public bool referencesFoldout = false;
        [NonSerialized] public bool monthDataFoldout = false;
        [NonSerialized] public bool eventsFoldout = false;
#endif
    }
}