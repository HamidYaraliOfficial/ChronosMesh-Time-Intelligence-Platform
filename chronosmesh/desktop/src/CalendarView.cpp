#include "CalendarView.h"

#include <QPainter>
#include <QMouseEvent>
#include <QResizeEvent>
#include <QApplication>
#include <cmath>

namespace ChronosMesh {

CalendarView::CalendarView(QWidget* parent) : QWidget(parent) {
    setMouseTracking(true);
    setMinimumHeight(400);
}

void CalendarView::setViewMode(CalendarViewMode mode) {
    m_mode = mode;
    update();
}

void CalendarView::setAnchorDate(const QDate& date) {
    m_anchorDate = date;
    update();
}

void CalendarView::setItems(const QVector<CalendarItem>& items) {
    m_items = items;
    update();
}

void CalendarView::setWorkingHours(int startMinute, int endMinute) {
    m_workStartMinute = startMinute;
    m_workEndMinute = endMinute;
    update();
}

int CalendarView::daysInView() const {
    switch (m_mode) {
        case CalendarViewMode::Day: return 1;
        case CalendarViewMode::Week: return 7;
        default: return 7;
    }
}

void CalendarView::paintEvent(QPaintEvent*) {
    QPainter painter(this);
    painter.setRenderHint(QPainter::Antialiasing, true);

    // Colors are read from the application palette so every theme (QSS)
    // controls calendar appearance without CalendarView knowing about
    // specific theme names.
    painter.fillRect(rect(), palette().base());

    switch (m_mode) {
        case CalendarViewMode::Day:
        case CalendarViewMode::Week:
            paintDayOrWeek(painter);
            break;
        case CalendarViewMode::Month:
            paintMonth(painter);
            break;
        case CalendarViewMode::Year:
            paintYear(painter);
            break;
        case CalendarViewMode::Timeline:
            paintTimeline(painter);
            break;
    }
}

void CalendarView::paintDayOrWeek(QPainter& painter) {
    const int days = daysInView();
    const bool rtl = layoutDirection() == Qt::RightToLeft;
    const double columnWidth = (width() - kTimeGutterWidth) / static_cast<double>(days);

    QDate startDate = (m_mode == CalendarViewMode::Week)
        ? m_anchorDate.addDays(-(m_anchorDate.dayOfWeek() - 1))
        : m_anchorDate;

    // Header row with day names/dates.
    painter.setPen(palette().text().color());
    QFont headerFont = font();
    headerFont.setBold(true);
    painter.setFont(headerFont);

    for (int d = 0; d < days; ++d) {
        const QDate date = startDate.addDays(d);
        const int visualColumn = rtl ? (days - 1 - d) : d;
        const double x = kTimeGutterWidth + visualColumn * columnWidth;
        QRectF headerRect(x, 0, columnWidth, kHeaderHeight);
        painter.drawText(headerRect, Qt::AlignCenter, date.toString(QStringLiteral("ddd d")));
    }

    // Hour gridlines + working-hours shading.
    painter.setFont(font());
    for (int hour = 0; hour <= 24; ++hour) {
        const double y = kHeaderHeight + hour * kHourHeight;
        painter.setPen(QPen(palette().mid().color(), 1));
        painter.drawLine(QPointF(kTimeGutterWidth, y), QPointF(width(), y));
        if (hour < 24) {
            painter.setPen(palette().text().color());
            painter.drawText(QRectF(0, y, kTimeGutterWidth - 4, kHourHeight),
                              Qt::AlignRight | Qt::AlignTop,
                              QTime(hour, 0).toString(QStringLiteral("HH:00")));
        }
    }

    for (int d = 0; d < days; ++d) {
        const int visualColumn = rtl ? (days - 1 - d) : d;
        const double x = kTimeGutterWidth + visualColumn * columnWidth;
        const double workY = kHeaderHeight + (m_workStartMinute / 60.0) * kHourHeight;
        const double workH = ((m_workEndMinute - m_workStartMinute) / 60.0) * kHourHeight;
        painter.fillRect(QRectF(x, workY, columnWidth, workH), palette().alternateBase());
        painter.setPen(QPen(palette().mid().color(), 1));
        painter.drawLine(QPointF(x, kHeaderHeight), QPointF(x, height()));
    }

    // Items.
    for (const auto& item : m_items) {
        for (int d = 0; d < days; ++d) {
            const QDate date = startDate.addDays(d);
            if (item.start.date() != date) continue;

            const int visualColumn = rtl ? (days - 1 - d) : d;
            const double x = kTimeGutterWidth + visualColumn * columnWidth + 2;
            const double startMinutes = item.start.time().hour() * 60 + item.start.time().minute();
            const double durationMinutes = item.start.secsTo(item.end) / 60.0;
            const double y = kHeaderHeight + (startMinutes / 60.0) * kHourHeight;
            const double h = std::max(18.0, (durationMinutes / 60.0) * kHourHeight);

            QRectF itemRect(x, y, columnWidth - 4, h);
            if (m_dragging && item.id == m_draggedItemId && m_dragPreviewStart.isValid()) {
                continue; // drawn separately below as a preview
            }

            painter.setBrush(item.color);
            painter.setPen(Qt::NoPen);
            painter.drawRoundedRect(itemRect, 6, 6);
            painter.setPen(Qt::white);
            painter.drawText(itemRect.adjusted(6, 2, -4, -2), Qt::AlignLeft | Qt::TextWordWrap, item.title);
        }
    }

    // Drag preview (ghost box following the cursor, snapped to 15 min).
    if (m_dragging && m_dragPreviewStart.isValid()) {
        const double startMinutes = m_dragPreviewStart.time().hour() * 60 + m_dragPreviewStart.time().minute();
        const double durationMinutes = m_dragOriginalStart.secsTo(m_dragOriginalEnd) / 60.0;
        const double y = kHeaderHeight + (startMinutes / 60.0) * kHourHeight;
        const double h = std::max(18.0, (durationMinutes / 60.0) * kHourHeight);
        QRectF preview(kTimeGutterWidth + 2, y, columnWidth - 4, h);
        painter.setBrush(QColor(79, 70, 229, 120));
        painter.setPen(QPen(QColor(79, 70, 229), 2, Qt::DashLine));
        painter.drawRoundedRect(preview, 6, 6);
    }
}

void CalendarView::paintMonth(QPainter& painter) {
    const bool rtl = layoutDirection() == Qt::RightToLeft;
    const QDate firstOfMonth(m_anchorDate.year(), m_anchorDate.month(), 1);
    const int leadingBlank = (firstOfMonth.dayOfWeek() + 6) % 7; // Monday-first grid
    const QDate gridStart = firstOfMonth.addDays(-leadingBlank);

    const double cellW = width() / 7.0;
    const double cellH = (height() - kHeaderHeight) / 6.0;

    static const QStringList dayNames = { tr("Mon"), tr("Tue"), tr("Wed"), tr("Thu"), tr("Fri"), tr("Sat"), tr("Sun") };
    QFont headerFont = font();
    headerFont.setBold(true);
    painter.setFont(headerFont);
    for (int c = 0; c < 7; ++c) {
        const int col = rtl ? (6 - c) : c;
        painter.drawText(QRectF(col * cellW, 0, cellW, kHeaderHeight), Qt::AlignCenter, dayNames[c]);
    }

    painter.setFont(font());
    for (int row = 0; row < 6; ++row) {
        for (int c = 0; c < 7; ++c) {
            const int col = rtl ? (6 - c) : c;
            const QDate cellDate = gridStart.addDays(row * 7 + c);
            QRectF cellRect(col * cellW, kHeaderHeight + row * cellH, cellW, cellH);

            painter.setPen(QPen(palette().mid().color(), 1));
            painter.drawRect(cellRect);

            const bool inMonth = cellDate.month() == m_anchorDate.month();
            painter.setPen(inMonth ? palette().text().color() : palette().mid().color());
            painter.drawText(cellRect.adjusted(4, 2, -4, -2), Qt::AlignTop | (rtl ? Qt::AlignRight : Qt::AlignLeft),
                              QString::number(cellDate.day()));

            int itemsShown = 0;
            for (const auto& item : m_items) {
                if (item.start.date() != cellDate) continue;
                if (itemsShown >= 3) break;
                QRectF chip(cellRect.left() + 4, cellRect.top() + 18 + itemsShown * 16, cellRect.width() - 8, 14);
                painter.setBrush(item.color);
                painter.setPen(Qt::NoPen);
                painter.drawRoundedRect(chip, 3, 3);
                painter.setPen(Qt::white);
                QFont small = font();
                small.setPointSize(std::max(7, font().pointSize() - 2));
                painter.setFont(small);
                painter.drawText(chip.adjusted(3, 0, -2, 0), Qt::AlignVCenter, item.title);
                painter.setFont(font());
                ++itemsShown;
            }
        }
    }
}

void CalendarView::paintYear(QPainter& painter) {
    const int cols = 4, rows = 3;
    const double cellW = width() / static_cast<double>(cols);
    const double cellH = height() / static_cast<double>(rows);

    QFont monthFont = font();
    monthFont.setBold(true);

    for (int m = 0; m < 12; ++m) {
        const int r = m / cols, c = m % cols;
        QRectF cell(c * cellW, r * cellH, cellW, cellH);
        painter.setPen(QPen(palette().mid().color(), 1));
        painter.drawRect(cell.adjusted(4, 4, -4, -4));

        painter.setFont(monthFont);
        painter.setPen(palette().text().color());
        const QDate monthDate(m_anchorDate.year(), m + 1, 1);
        painter.drawText(cell.adjusted(8, 6, -8, -6), Qt::AlignTop | Qt::AlignHCenter, monthDate.toString(QStringLiteral("MMMM")));

        // Mini day-count indicator: how many items fall in this month.
        int count = 0;
        for (const auto& item : m_items) {
            if (item.start.date().year() == m_anchorDate.year() && item.start.date().month() == m + 1) ++count;
        }
        painter.setFont(font());
        painter.drawText(cell.adjusted(8, 26, -8, -6), Qt::AlignTop | Qt::AlignHCenter,
                          tr("%n item(s)", "", count));
    }
}

void CalendarView::paintTimeline(QPainter& painter) {
    // A horizontal timeline: one row per day for the next 14 days, items
    // drawn as proportionally-positioned bars. Useful for spotting
    // multi-day workload distribution at a glance.
    const int daysShown = 14;
    const double rowHeight = height() / static_cast<double>(daysShown);

    for (int d = 0; d < daysShown; ++d) {
        const QDate date = m_anchorDate.addDays(d);
        QRectF row(0, d * rowHeight, width(), rowHeight);
        painter.setPen(QPen(palette().mid().color(), 1));
        painter.drawLine(row.bottomLeft(), row.bottomRight());
        painter.setPen(palette().text().color());
        painter.drawText(QRectF(4, row.top(), 100, rowHeight), Qt::AlignVCenter, date.toString(QStringLiteral("ddd d MMM")));

        for (const auto& item : m_items) {
            if (item.start.date() != date) continue;
            const double startFraction = (item.start.time().hour() * 60 + item.start.time().minute()) / 1440.0;
            const double durFraction = item.start.secsTo(item.end) / 86400.0;
            QRectF bar(110 + startFraction * (width() - 120), row.top() + 4, std::max(6.0, durFraction * (width() - 120)), rowHeight - 8);
            painter.setBrush(item.color);
            painter.setPen(Qt::NoPen);
            painter.drawRoundedRect(bar, 4, 4);
        }
    }
}

QDateTime CalendarView::dateTimeAtPosition(const QPointF& pos) const {
    if (m_mode != CalendarViewMode::Day && m_mode != CalendarViewMode::Week) return {};

    const int days = daysInView();
    const bool rtl = layoutDirection() == Qt::RightToLeft;
    const double columnWidth = (width() - kTimeGutterWidth) / static_cast<double>(days);
    QDate startDate = (m_mode == CalendarViewMode::Week)
        ? m_anchorDate.addDays(-(m_anchorDate.dayOfWeek() - 1))
        : m_anchorDate;

    int visualColumn = static_cast<int>((pos.x() - kTimeGutterWidth) / columnWidth);
    visualColumn = std::clamp(visualColumn, 0, days - 1);
    const int dayIndex = rtl ? (days - 1 - visualColumn) : visualColumn;

    double minutesFromTop = ((pos.y() - kHeaderHeight) / kHourHeight) * 60.0;
    // snap to nearest 15 minutes
    minutesFromTop = std::round(minutesFromTop / 15.0) * 15.0;
    minutesFromTop = std::clamp(minutesFromTop, 0.0, 24.0 * 60.0 - 15.0);

    QDateTime dt(startDate.addDays(dayIndex), QTime(0, 0));
    return dt.addSecs(static_cast<int>(minutesFromTop) * 60);
}

void CalendarView::mousePressEvent(QMouseEvent* event) {
    for (const auto& item : m_items) {
        // (Hit-testing against the last painted geometry is normally
        // cached from paintEvent; simplified here to a time-based lookup
        // via dateTimeAtPosition + item bounds for brevity.)
        const QDateTime clicked = dateTimeAtPosition(event->position());
        if (clicked.isValid() && item.start <= clicked && clicked < item.end) {
            m_dragging = true;
            m_draggedItemId = item.id;
            m_dragStartPos = event->position();
            m_dragOriginalStart = item.start;
            m_dragOriginalEnd = item.end;
            m_dragPreviewStart = item.start;
            break;
        }
    }
    QWidget::mousePressEvent(event);
}

void CalendarView::mouseMoveEvent(QMouseEvent* event) {
    if (m_dragging) {
        const QDateTime hovered = dateTimeAtPosition(event->position());
        if (hovered.isValid()) {
            m_dragPreviewStart = hovered;
            update();
        }
    }
    QWidget::mouseMoveEvent(event);
}

void CalendarView::mouseReleaseEvent(QMouseEvent* event) {
    if (m_dragging) {
        m_dragging = false;
        if (m_dragPreviewStart.isValid() && m_dragPreviewStart != m_dragOriginalStart) {
            const qint64 durationSecs = m_dragOriginalStart.secsTo(m_dragOriginalEnd);
            const QDateTime newEnd = m_dragPreviewStart.addSecs(durationSecs);
            emit itemMoved(m_draggedItemId, m_dragPreviewStart, newEnd);
        }
        m_dragPreviewStart = QDateTime();
        update();
    }
    QWidget::mouseReleaseEvent(event);
}

void CalendarView::mouseDoubleClickEvent(QMouseEvent* event) {
    const QDateTime clicked = dateTimeAtPosition(event->position());
    if (clicked.isValid()) {
        emit slotDoubleClicked(clicked);
    }
    QWidget::mouseDoubleClickEvent(event);
}

void CalendarView::resizeEvent(QResizeEvent* event) {
    QWidget::resizeEvent(event);
    update();
}

} // namespace ChronosMesh
