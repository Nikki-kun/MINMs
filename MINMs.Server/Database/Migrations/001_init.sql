#SET NAMES utf8mb4;

CREATE TABLE `users` (
	`user_id` INT NOT NULL AUTO_INCREMENT,
	`username` VARCHAR(100) NOT NULL,
	`login` VARCHAR(32) NOT NULL,
	`password_hash` VARCHAR(255) NOT NULL,
	`online` TINYINT(1) NOT NULL DEFAULT 0,
	`user_last_seen` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	`user_created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	PRIMARY KEY (`user_id`),
	UNIQUE KEY `uk_users_login` (`login`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `files` (
	`file_id` INT NOT NULL AUTO_INCREMENT,
	`owner_user_id` INT NULL,
	`original_filename` VARCHAR(500) NOT NULL,
	`remote_path` VARCHAR(2048) NOT NULL,
	`size_bytes` BIGINT NULL,
	`content_type` VARCHAR(255) NULL,
	`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	PRIMARY KEY (`file_id`),
	KEY `idx_files_owner` (`owner_user_id`),
	CONSTRAINT `fk_files_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`user_id`)
		ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `contacts` (
	`owner_id` INT NOT NULL,
	`contact_id` INT NOT NULL,
	`contact_name` VARCHAR(100) NOT NULL,
	`contact_added_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	PRIMARY KEY (`owner_id`, `contact_id`),
	KEY `idx_contacts_owner` (`owner_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `blocked_users` (
	`user_id` INT NOT NULL,
	`blocked_user_id` INT NOT NULL,
	`blocked_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	PRIMARY KEY (`user_id`, `blocked_user_id`),
	KEY `idx_blocked_users_blocked` (`blocked_user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `chats` (
	`chat_id` INT NOT NULL AUTO_INCREMENT,
	`type` TINYINT NOT NULL,
	`chat_created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	PRIMARY KEY (`chat_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `chat_participants` (
	`chat_id` INT NOT NULL,
	`user_id` INT NOT NULL,
	`participant_role` TINYINT NOT NULL DEFAULT 2,
	`membership_status` TINYINT NOT NULL DEFAULT 0,
	`joined_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	`left_at` DATETIME NULL,
	`banned_at` DATETIME NULL,
	PRIMARY KEY (`chat_id`, `user_id`),
	KEY `idx_chat_participants_user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `messages` (
	`message_id` INT NOT NULL AUTO_INCREMENT,
	`sender_id` INT NOT NULL,
	`chat_id` INT NOT NULL,
	`content` VARCHAR(1000) NOT NULL,
	`message_created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	`status` TINYINT NOT NULL DEFAULT 0,
	`type` TINYINT NOT NULL DEFAULT 0,
	PRIMARY KEY (`message_id`),
	KEY `idx_messages_chat` (`chat_id`),
	KEY `idx_messages_sender` (`sender_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE `contacts`
	ADD CONSTRAINT `fk_contacts_owner` FOREIGN KEY (`owner_id`) REFERENCES `users` (`user_id`)
		ON DELETE CASCADE ON UPDATE RESTRICT,
	ADD CONSTRAINT `fk_contacts_contact` FOREIGN KEY (`contact_id`) REFERENCES `users` (`user_id`)
		ON DELETE CASCADE ON UPDATE RESTRICT;

ALTER TABLE `blocked_users`
	ADD CONSTRAINT `fk_blocked_users_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`)
		ON DELETE CASCADE ON UPDATE RESTRICT,
	ADD CONSTRAINT `fk_blocked_users_blocked` FOREIGN KEY (`blocked_user_id`) REFERENCES `users` (`user_id`)
		ON DELETE CASCADE ON UPDATE RESTRICT;

ALTER TABLE `chat_participants`
	ADD CONSTRAINT `fk_chat_participants_chat` FOREIGN KEY (`chat_id`) REFERENCES `chats` (`chat_id`)
		ON DELETE CASCADE ON UPDATE RESTRICT,
	ADD CONSTRAINT `fk_chat_participants_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`)
		ON DELETE CASCADE ON UPDATE RESTRICT;

ALTER TABLE `messages`
	ADD CONSTRAINT `fk_messages_sender` FOREIGN KEY (`sender_id`) REFERENCES `users` (`user_id`)
		ON DELETE CASCADE ON UPDATE RESTRICT,
	ADD CONSTRAINT `fk_messages_chat` FOREIGN KEY (`chat_id`) REFERENCES `chats` (`chat_id`)
		ON DELETE CASCADE ON UPDATE RESTRICT;
