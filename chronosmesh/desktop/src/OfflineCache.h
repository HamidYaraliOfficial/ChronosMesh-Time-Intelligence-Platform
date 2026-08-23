#pragma once

#include <QObject>
#include <QSqlDatabase>
#include <QString>
#include <QJsonObject>
#include <QDateTime>
#include <QVector>

namespace ChronosMesh {

/// Local SQLite-backed cache enabling Offline Mode: every entity fetched
/// from the API is mirrored here, and local edits made while offline are
/// queued in `pending_changes` until connectivity returns. On reconnect,
/// MainWindow drains the queue and resolves any conflicts by delegating to
/// the Rust Secure Core's conflict-resolution logic (last-write-wins with
/// device-clock tiebreaker), never by silently discarding local edits.
class OfflineCache : public QObject {
    Q_OBJECT

public:
    explicit OfflineCache(QObject* parent = nullptr);

    bool open(const QString& sqliteFilePath);

    void upsertEntity(const QString& entityType, const QString& entityId, const QJsonObject& payload, const QDateTime& updatedAtUtc);
    QJsonObject getEntity(const QString& entityType, const QString& entityId) const;

    void queuePendingChange(const QString& entityType, const QString& entityId, const QJsonObject& payload);
    QVector<QJsonObject> pendingChanges() const;
    void clearPendingChange(const QString& entityType, const QString& entityId);

    bool isOnline() const { return m_online; }
    void setOnline(bool online);

signals:
    void connectivityChanged(bool online);

private:
    void ensureSchema();

    QSqlDatabase m_db;
    bool m_online = true;
};

} // namespace ChronosMesh
