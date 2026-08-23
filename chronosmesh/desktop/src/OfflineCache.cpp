#include "OfflineCache.h"

#include <QSqlQuery>
#include <QSqlError>
#include <QJsonDocument>
#include <QDebug>
#include <QUuid>

namespace ChronosMesh {

OfflineCache::OfflineCache(QObject* parent) : QObject(parent) {}

bool OfflineCache::open(const QString& sqliteFilePath) {
    m_db = QSqlDatabase::addDatabase(QStringLiteral("QSQLITE"), QStringLiteral("chronosmesh_offline"));
    m_db.setDatabaseName(sqliteFilePath);
    if (!m_db.open()) {
        qWarning() << "OfflineCache: failed to open" << sqliteFilePath << m_db.lastError().text();
        return false;
    }
    ensureSchema();
    return true;
}

void OfflineCache::ensureSchema() {
    QSqlQuery query(m_db);
    query.exec(QStringLiteral(
        "CREATE TABLE IF NOT EXISTS cached_entities ("
        "entity_type TEXT NOT NULL,"
        "entity_id TEXT NOT NULL,"
        "payload_json TEXT NOT NULL,"
        "updated_at_utc TEXT NOT NULL,"
        "PRIMARY KEY (entity_type, entity_id))"));

    query.exec(QStringLiteral(
        "CREATE TABLE IF NOT EXISTS pending_changes ("
        "id TEXT PRIMARY KEY,"
        "entity_type TEXT NOT NULL,"
        "entity_id TEXT NOT NULL,"
        "payload_json TEXT NOT NULL,"
        "created_at_utc TEXT NOT NULL)"));
}

void OfflineCache::upsertEntity(const QString& entityType, const QString& entityId, const QJsonObject& payload, const QDateTime& updatedAtUtc) {
    QSqlQuery query(m_db);
    query.prepare(QStringLiteral(
        "INSERT INTO cached_entities (entity_type, entity_id, payload_json, updated_at_utc) "
        "VALUES (:type, :id, :payload, :updated) "
        "ON CONFLICT(entity_type, entity_id) DO UPDATE SET payload_json = excluded.payload_json, updated_at_utc = excluded.updated_at_utc"));
    query.bindValue(QStringLiteral(":type"), entityType);
    query.bindValue(QStringLiteral(":id"), entityId);
    query.bindValue(QStringLiteral(":payload"), QString::fromUtf8(QJsonDocument(payload).toJson(QJsonDocument::Compact)));
    query.bindValue(QStringLiteral(":updated"), updatedAtUtc.toUTC().toString(Qt::ISODate));
    if (!query.exec()) {
        qWarning() << "OfflineCache::upsertEntity failed:" << query.lastError().text();
    }
}

QJsonObject OfflineCache::getEntity(const QString& entityType, const QString& entityId) const {
    QSqlQuery query(m_db);
    query.prepare(QStringLiteral("SELECT payload_json FROM cached_entities WHERE entity_type = :type AND entity_id = :id"));
    query.bindValue(QStringLiteral(":type"), entityType);
    query.bindValue(QStringLiteral(":id"), entityId);
    if (query.exec() && query.next()) {
        const QByteArray raw = query.value(0).toByteArray();
        return QJsonDocument::fromJson(raw).object();
    }
    return {};
}

void OfflineCache::queuePendingChange(const QString& entityType, const QString& entityId, const QJsonObject& payload) {
    QSqlQuery query(m_db);
    query.prepare(QStringLiteral(
        "INSERT INTO pending_changes (id, entity_type, entity_id, payload_json, created_at_utc) "
        "VALUES (:id, :type, :entityId, :payload, :created)"));
    query.bindValue(QStringLiteral(":id"), QUuid::createUuid().toString(QUuid::WithoutBraces));
    query.bindValue(QStringLiteral(":type"), entityType);
    query.bindValue(QStringLiteral(":entityId"), entityId);
    query.bindValue(QStringLiteral(":payload"), QString::fromUtf8(QJsonDocument(payload).toJson(QJsonDocument::Compact)));
    query.bindValue(QStringLiteral(":created"), QDateTime::currentDateTimeUtc().toString(Qt::ISODate));
    if (!query.exec()) {
        qWarning() << "OfflineCache::queuePendingChange failed:" << query.lastError().text();
    }
}

QVector<QJsonObject> OfflineCache::pendingChanges() const {
    QVector<QJsonObject> result;
    QSqlQuery query(QStringLiteral("SELECT entity_type, entity_id, payload_json FROM pending_changes ORDER BY created_at_utc ASC"), m_db);
    while (query.next()) {
        QJsonObject obj;
        obj[QStringLiteral("entity_type")] = query.value(0).toString();
        obj[QStringLiteral("entity_id")] = query.value(1).toString();
        obj[QStringLiteral("payload")] = QJsonDocument::fromJson(query.value(2).toByteArray()).object();
        result.append(obj);
    }
    return result;
}

void OfflineCache::clearPendingChange(const QString& entityType, const QString& entityId) {
    QSqlQuery query(m_db);
    query.prepare(QStringLiteral("DELETE FROM pending_changes WHERE entity_type = :type AND entity_id = :id"));
    query.bindValue(QStringLiteral(":type"), entityType);
    query.bindValue(QStringLiteral(":id"), entityId);
    query.exec();
}

void OfflineCache::setOnline(bool online) {
    if (m_online == online) return;
    m_online = online;
    emit connectivityChanged(online);
}

} // namespace ChronosMesh
