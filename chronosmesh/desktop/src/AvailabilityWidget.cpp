#include "AvailabilityWidget.h"

#include <QCheckBox>
#include <QTimeEdit>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QGroupBox>
#include <QScrollArea>
#include <QFrame>

namespace ChronosMesh {

WorkingDayRow::WorkingDayRow(int weekday, const QString& dayLabel, QWidget* parent)
    : QWidget(parent), m_weekday(weekday) {
    auto* outer = new QVBoxLayout(this);
    auto* topRow = new QHBoxLayout();

    m_enabledCheck = new QCheckBox(dayLabel, this);
    m_enabledCheck->setChecked(weekday < 5); // Mon-Fri enabled by default
    m_enabledCheck->setMinimumWidth(120);

    m_startEdit = new QTimeEdit(QTime(9, 0), this);
    m_startEdit->setDisplayFormat(QStringLiteral("HH:mm"));
    m_endEdit = new QTimeEdit(QTime(17, 0), this);
    m_endEdit->setDisplayFormat(QStringLiteral("HH:mm"));

    m_addBreakButton = new QPushButton(tr("+ Break"), this);
    m_addBreakButton->setFlat(true);

    topRow->addWidget(m_enabledCheck);
    topRow->addWidget(new QLabel(tr("From"), this));
    topRow->addWidget(m_startEdit);
    topRow->addWidget(new QLabel(tr("To"), this));
    topRow->addWidget(m_endEdit);
    topRow->addStretch();
    topRow->addWidget(m_addBreakButton);

    outer->addLayout(topRow);

    m_breaksLayout = new QVBoxLayout();
    outer->addLayout(m_breaksLayout);

    connect(m_enabledCheck, &QCheckBox::toggled, this, [this](bool on) {
        m_startEdit->setEnabled(on);
        m_endEdit->setEnabled(on);
        m_addBreakButton->setEnabled(on);
        emit changed();
    });
    connect(m_startEdit, &QTimeEdit::timeChanged, this, &WorkingDayRow::changed);
    connect(m_endEdit, &QTimeEdit::timeChanged, this, &WorkingDayRow::changed);
    connect(m_addBreakButton, &QPushButton::clicked, this, [this] { addBreakRow(); });
}

void WorkingDayRow::addBreakRow(QTime start, QTime end) {
    auto* row = new QWidget(this);
    auto* layout = new QHBoxLayout(row);
    layout->setContentsMargins(24, 0, 0, 0);

    auto* label = new QLabel(tr("Break"), row);
    auto* startEdit = new QTimeEdit(start, row);
    startEdit->setDisplayFormat(QStringLiteral("HH:mm"));
    auto* endEdit = new QTimeEdit(end, row);
    endEdit->setDisplayFormat(QStringLiteral("HH:mm"));
    auto* removeButton = new QPushButton(tr("Remove"), row);
    removeButton->setFlat(true);

    layout->addWidget(label);
    layout->addWidget(startEdit);
    layout->addWidget(new QLabel(tr("–"), row));
    layout->addWidget(endEdit);
    layout->addStretch();
    layout->addWidget(removeButton);

    m_breaksLayout->addWidget(row);
    m_breakEdits.append({ startEdit, endEdit });

    connect(startEdit, &QTimeEdit::timeChanged, this, &WorkingDayRow::changed);
    connect(endEdit, &QTimeEdit::timeChanged, this, &WorkingDayRow::changed);
    connect(removeButton, &QPushButton::clicked, this, [this, row, startEdit, endEdit] {
        m_breakEdits.removeAll({ startEdit, endEdit });
        row->deleteLater();
        emit changed();
    });

    emit changed();
}

bool WorkingDayRow::isEnabled_() const { return m_enabledCheck->isChecked(); }
QTime WorkingDayRow::startTime() const { return m_startEdit->time(); }
QTime WorkingDayRow::endTime() const { return m_endEdit->time(); }

QVector<QPair<QTime, QTime>> WorkingDayRow::breaks() const {
    QVector<QPair<QTime, QTime>> result;
    for (const auto& pair : m_breakEdits) {
        result.append({ pair.first->time(), pair.second->time() });
    }
    return result;
}

// ---------------------------------------------------------------------

AvailabilityWidget::AvailabilityWidget(QWidget* parent) : QWidget(parent) {
    auto* mainLayout = new QVBoxLayout(this);

    auto* title = new QLabel(tr("Working Hours & Availability"), this);
    QFont titleFont = title->font();
    titleFont.setPointSize(titleFont.pointSize() + 4);
    titleFont.setBold(true);
    title->setFont(titleFont);
    mainLayout->addWidget(title);

    auto* subtitle = new QLabel(
        tr("Tell ChronosMesh which days you work, your hours, and your breaks. "
           "The Time Engine calculates exactly how much free time you have left."), this);
    subtitle->setWordWrap(true);
    mainLayout->addWidget(subtitle);

    auto* scrollArea = new QScrollArea(this);
    scrollArea->setWidgetResizable(true);
    auto* daysContainer = new QWidget(scrollArea);
    auto* daysLayout = new QVBoxLayout(daysContainer);

    static const QStringList dayLabels = {
        tr("Monday"), tr("Tuesday"), tr("Wednesday"), tr("Thursday"),
        tr("Friday"), tr("Saturday"), tr("Sunday")
    };

    for (int i = 0; i < 7; ++i) {
        auto* row = new WorkingDayRow(i, dayLabels[i], daysContainer);
        m_rows.append(row);
        daysLayout->addWidget(row);

        auto* separator = new QFrame(daysContainer);
        separator->setFrameShape(QFrame::HLine);
        daysLayout->addWidget(separator);
    }
    daysLayout->addStretch();
    scrollArea->setWidget(daysContainer);
    mainLayout->addWidget(scrollArea, 1);

    auto* summaryBox = new QGroupBox(tr("Live Availability Summary"), this);
    auto* summaryLayout = new QVBoxLayout(summaryBox);
    m_summaryLabel = new QLabel(tr("Save your schedule to see next available slot, "
                                    "total free time today, and remaining working time."), summaryBox);
    m_summaryLabel->setWordWrap(true);
    summaryLayout->addWidget(m_summaryLabel);
    mainLayout->addWidget(summaryBox);

    m_saveButton = new QPushButton(tr("Save Working Hours"), this);
    m_saveButton->setObjectName(QStringLiteral("primaryButton"));
    mainLayout->addWidget(m_saveButton, 0, Qt::AlignRight);

    connect(m_saveButton, &QPushButton::clicked, this, &AvailabilityWidget::saveRequested);
}

QVector<AvailabilityWidget::WorkingDayData> AvailabilityWidget::collectSchedule() const {
    QVector<WorkingDayData> result;
    for (auto* row : m_rows) {
        if (!row->isEnabled_()) continue;
        WorkingDayData data;
        data.weekday = row->weekday();
        data.startMinute = row->startTime().hour() * 60 + row->startTime().minute();
        data.endMinute = row->endTime().hour() * 60 + row->endTime().minute();
        for (const auto& brk : row->breaks()) {
            data.breaksMinutes.append({ brk.first.hour() * 60 + brk.first.minute(),
                                         brk.second.hour() * 60 + brk.second.minute() });
        }
        result.append(data);
    }
    return result;
}

void AvailabilityWidget::showAvailabilitySummary(const QString& summaryText) {
    m_summaryLabel->setText(summaryText);
}

} // namespace ChronosMesh
