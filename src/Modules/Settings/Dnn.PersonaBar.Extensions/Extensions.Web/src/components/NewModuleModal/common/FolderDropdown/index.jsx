import React, { PropTypes, Component } from "react";
import DropdownWithError from "dnn-dropdown-with-error";
import SingleLineInputWithError from "dnn-single-line-input-with-error";
import Button from "dnn-button";
import GridCell from "dnn-grid-cell";
import Collapsible from "react-collapse";
import Localization from "localization";
import styles from "./style.less";

class FolderDropdown extends Component {
    constructor() {
        super();
        this.state = {
            addNewFolderOpen: false,
            newFolderValue: ""
        };
    }

    onOwnerFolderSelect(option) {
        const { props } = this;
        if (option && option.value === "AddNewFolder") {
            this.setState({
                addNewFolderOpen: true
            });
        } else {
            props.onFolderSelect(option);
        }
    }

    closeAddNewFolderBox() {
        this.setState({
            addNewFolderOpen: false,
            newFolderValue: ""
        });
    }

    onAddNewFolder() {
        const { props, state } = this;

        props.onAddNewFolder(state.newFolderValue, props.type, this.closeAddNewFolderBox.bind(this));
    }

    onFolderNameChange(event) {
        this.setState({
            newFolderValue: event.target.value
        });
    }

    render() {
        const { props, state } = this;
        const folders = [
            {
                label: "[" + Localization.get("CreateNewModule_NewFolder.Label") + "]",
                value: "AddNewFolder"
            },
            {
                label: Localization.get("CreateNewModule_NotSpecified.Label"),
                value: ""
            }
        ].concat(props.folders);
        return (
            <GridCell className={styles.folderDropdown} style={{ padding: 0 }}>
                <Collapsible isOpened={state.addNewFolderOpen} className="add-new-folder-box" fixedHeight={250} style={{ float: "left" }}>
                    <GridCell style={{ padding: "20px 10px" }}>
                        <GridCell>
                            <h3 className="new-folder-title">{Localization.get("CreateNewModule_NewFolder.Label")}</h3>
                            <SingleLineInputWithError
                                label={Localization.get("CreateNewModule_FolderName.Label")}
                                inputStyle={{ marginBottom: 16 }}
                                value={state.newFolderValue}
                                onChange={this.onFolderNameChange.bind(this)}
                                />
                        </GridCell>
                        <GridCell className="new-folder-buttons">
                            <Button type="secondary" onClick={this.closeAddNewFolderBox.bind(this)}>{Localization.get("NewModule_Cancel.Button")}</Button>
                            <Button type="primary" onClick={this.onAddNewFolder.bind(this)}>{Localization.get("NewModule_Create.Button")}</Button>
                        </GridCell>
                    </GridCell>
                </Collapsible>
                <DropdownWithError
                    options={folders}
                    tooltipMessage={props.tooltipMessage}
                    label={props.label}
                    className={"folder-dropdown" + (state.addNewFolderOpen ? " hidden" : "")}
                    onSelect={this.onOwnerFolderSelect.bind(this)}
                    value={props.value}
                    enabled={props.enabled}
                    error={props.error}
                    />
            </GridCell>
        );
    }
}

FolderDropdown.PropTypes = {
    onCancel: PropTypes.func,
    folders: PropTypes.array,
    onFolderSelect: PropTypes.func,
    type: PropTypes.string,
    tooltipMessage: PropTypes.string,
    label: PropTypes.string,
    enabled: PropTypes.bool,
    error: PropTypes.bool
};

export default FolderDropdown;