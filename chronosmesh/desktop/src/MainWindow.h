#pragma once

#include <QMainWindow>
#include <QStackedWidget>
#include <QListWidget>

#include "CalendarView.h"
#include "AvailabilityWidget.h"
#include "CommandPalette.h"
#include "ApiClient.h"
#include "OfflineCache.h"
#include "TranslationManager.h"

namespace ChronosMesh {

/// Top-level application shell: sidebar navigation, stacked content pages
/// (Dashboard, Calendar, Timeline, Tasks, Projects, Schedules,
/// Availability, Bookings, Analytics, Notifications, Rules, Automation,
/// Settings, Security, Profile, Workspace, Search), theme/language
/// switching, and the Ctrl+K command palette.
class MainWindow : public QMainWindow {
    Q_OBJECT

public:
    explicit MainWindow(QWidget* parent = nullptr);

private slots:
    void onSidebarItemChanged(int row);
    void onThemeSelected(const QString& themeName);
    void onLanguageSelected(const QString& langCode);
    void openCommandPalette();
    void onCommandTriggered(const QString& commandId);
    void onAvailabilitySaveRequested();

private:
    void buildSidebar();
    void buildPages();
    void buildMenus();
    void buildStatusBar();
    void retranslateUi();

    QListWidget* m_sidebar;
    QStackedWidget* m_pages;
    CalendarView* m_calendarView;
    AvailabilityWidget* m_availabilityWidget;
    CommandPalette* m_commandPalette;

    ApiClient* m_apiClient;
    OfflineCache* m_offlineCache;

    QAction* m_offlineIndicatorAction = nullptr;
};

} // namespace ChronosMesh
