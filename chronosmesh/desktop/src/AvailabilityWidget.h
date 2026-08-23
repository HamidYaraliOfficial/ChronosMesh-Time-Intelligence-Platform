#pragma once

#include <QWidget>
#include <QVector>
#include <QTime>

class QCheckBox;
class QTimeEdit;
class QVBoxLayout;
class QLabel;
class QPushButton;

namespace ChronosMesh {

/// One editable row: a day of the week, whether the user works that day,
/// start/end time, and an editable list of break windows. This is the
/// concrete UI for the product requirement that the user can declare
/// exactly which days they work, their hours, and their breaks, with the
/// system then computing real remaining free time (via the Rust Time
/// Engine over the API).
class WorkingDayRow : public QWidget {
    Q_OBJECT
public:
    explicit WorkingDayRow(int weekday, const QString& dayLabel, QWidget* parent = nullptr);

    bool isEnabled_() const;
    QTime startTime() const;
    QTime endTime() const;
    QVector<QPair<QTime, QTime>> breaks() const;
    int weekday() const { return m_weekday; }

    void addBreakRow(QTime start = QTime(12, 30), QTime end = QTime(13, 30));

signals:
    void changed();

private:
    int m_weekday;
    QCheckBox* m_enabledCheck;
    QTimeEdit* m_startEdit;
    QTimeEdit* m_endEdit;
    QVBoxLayout* m_breaksLayout;
    QPushButton* m_addBreakButton;
    QVector<QPair<QTimeEdit*, QTimeEdit*>> m_breakEdits;
};

/// Full working-hours / availability configuration screen: seven
/// WorkingDayRow entries plus a live-computed "free time remaining" panel
/// (populated from GET /api/v1/availability/me/summary once saved).
class AvailabilityWidget : public QWidget {
    Q_OBJECT

public:
    explicit AvailabilityWidget(QWidget* parent = nullptr);

    struct WorkingDayData {
        int weekday;
        int startMinute;
        int endMinute;
        QVector<QPair<int, int>> breaksMinutes;
    };
    QVector<WorkingDayData> collectSchedule() const;

    void showAvailabilitySummary(const QString& summaryText);

signals:
    void saveRequested();

private:
    QVector<WorkingDayRow*> m_rows;
    QLabel* m_summaryLabel;
    QPushButton* m_saveButton;
};

} // namespace ChronosMesh
