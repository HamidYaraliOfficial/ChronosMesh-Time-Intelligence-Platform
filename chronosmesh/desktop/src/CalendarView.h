#pragma once

#include <QWidget>
#include <QDate>
#include <QDateTime>
#include <QVector>
#include <QString>
#include <QUuid>

namespace ChronosMesh {

enum class CalendarViewMode { Day, Week, Month, Year, Timeline };

/// A single renderable calendar entry (event, task chunk, or booking).
struct CalendarItem {
    QUuid id;
    QString title;
    QDateTime start;
    QDateTime end;
    QColor color { 79, 70, 229 }; // ChronosMesh indigo default
    bool isTask = false;
};

/// Custom-painted calendar surface supporting Day/Week/Month/Year/Timeline
/// modes with drag & drop rescheduling. Deliberately implemented with
/// QPainter rather than QCalendarWidget so ChronosMesh can render
/// multi-item overlapping events, working-hour shading, and drag previews
/// that QCalendarWidget does not support.
class CalendarView : public QWidget {
    Q_OBJECT

public:
    explicit CalendarView(QWidget* parent = nullptr);

    void setViewMode(CalendarViewMode mode);
    CalendarViewMode viewMode() const { return m_mode; }

    void setAnchorDate(const QDate& date);
    QDate anchorDate() const { return m_anchorDate; }

    void setItems(const QVector<CalendarItem>& items);
    void setWorkingHours(int startMinute, int endMinute); // shades non-working time

signals:
    void itemMoved(const QUuid& itemId, const QDateTime& newStart, const QDateTime& newEnd);
    void itemActivated(const QUuid& itemId);
    void slotDoubleClicked(const QDateTime& start);

protected:
    void paintEvent(QPaintEvent* event) override;
    void mousePressEvent(QMouseEvent* event) override;
    void mouseMoveEvent(QMouseEvent* event) override;
    void mouseReleaseEvent(QMouseEvent* event) override;
    void mouseDoubleClickEvent(QMouseEvent* event) override;
    void resizeEvent(QResizeEvent* event) override;

private:
    void paintDayOrWeek(QPainter& painter);
    void paintMonth(QPainter& painter);
    void paintYear(QPainter& painter);
    void paintTimeline(QPainter& painter);

    QRectF rectForItem(const CalendarItem& item, int dayColumn, int totalColumns) const;
    QDateTime dateTimeAtPosition(const QPointF& pos) const;
    int daysInView() const;

    CalendarViewMode m_mode = CalendarViewMode::Week;
    QDate m_anchorDate = QDate::currentDate();
    QVector<CalendarItem> m_items;

    int m_workStartMinute = 8 * 60;
    int m_workEndMinute = 17 * 60;

    // Drag state
    bool m_dragging = false;
    QUuid m_draggedItemId;
    QPointF m_dragStartPos;
    QDateTime m_dragOriginalStart;
    QDateTime m_dragOriginalEnd;
    QDateTime m_dragPreviewStart;

    static constexpr int kHourHeight = 56;
    static constexpr int kHeaderHeight = 40;
    static constexpr int kTimeGutterWidth = 56;
};

} // namespace ChronosMesh
