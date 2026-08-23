#include "CommandPalette.h"

#include <QLineEdit>
#include <QListWidget>
#include <QVBoxLayout>
#include <QKeyEvent>

namespace ChronosMesh {

CommandPalette::CommandPalette(QWidget* parent) : QDialog(parent) {
    setWindowFlag(Qt::FramelessWindowHint);
    setWindowFlag(Qt::Popup);
    setMinimumWidth(520);

    auto* layout = new QVBoxLayout(this);
    m_input = new QLineEdit(this);
    m_input->setPlaceholderText(tr("Type a command or search ChronosMesh…"));
    m_list = new QListWidget(this);
    m_list->setMinimumHeight(280);

    layout->addWidget(m_input);
    layout->addWidget(m_list);

    connect(m_input, &QLineEdit::textChanged, this, &CommandPalette::onTextChanged);
    connect(m_input, &QLineEdit::returnPressed, this, &CommandPalette::onActivated);
    connect(m_list, &QListWidget::itemActivated, this, &CommandPalette::onActivated);
}

void CommandPalette::setCommands(const QVector<Command>& commands) {
    m_allCommands = commands;
    applyFilter(QString());
}

void CommandPalette::open() {
    m_input->clear();
    applyFilter(QString());
    m_input->setFocus();
    show();
    raise();
    activateWindow();
}

void CommandPalette::onTextChanged(const QString& text) {
    applyFilter(text);
}

void CommandPalette::applyFilter(const QString& text) {
    m_list->clear();
    const QString needle = text.trimmed().toLower();
    for (const auto& cmd : m_allCommands) {
        if (needle.isEmpty() || cmd.label.toLower().contains(needle) || cmd.category.toLower().contains(needle)) {
            auto* item = new QListWidgetItem(QStringLiteral("%1  ·  %2").arg(cmd.label, cmd.category));
            item->setData(Qt::UserRole, cmd.id);
            m_list->addItem(item);
        }
    }
    if (m_list->count() > 0) {
        m_list->setCurrentRow(0);
    }
}

void CommandPalette::onActivated() {
    auto* item = m_list->currentItem();
    if (!item) return;
    const QString id = item->data(Qt::UserRole).toString();
    emit commandTriggered(id);
    close();
}

} // namespace ChronosMesh
