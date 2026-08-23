#pragma once

#include <QDialog>
#include <QVector>
#include <QString>

class QLineEdit;
class QListWidget;

namespace ChronosMesh {

/// Ctrl+K / Cmd+K command palette: fuzzy-filters a flat list of registered
/// commands (navigate to page, create task, switch theme, switch language,
/// etc.) and executes the chosen one on Enter.
class CommandPalette : public QDialog {
    Q_OBJECT
public:
    struct Command {
        QString id;
        QString label;
        QString category;
    };

    explicit CommandPalette(QWidget* parent = nullptr);

    void setCommands(const QVector<Command>& commands);
    void open();

signals:
    void commandTriggered(const QString& commandId);

private slots:
    void onTextChanged(const QString& text);
    void onActivated();

private:
    void applyFilter(const QString& text);

    QLineEdit* m_input;
    QListWidget* m_list;
    QVector<Command> m_allCommands;
};

} // namespace ChronosMesh
